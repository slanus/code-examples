using System.Collections.Generic;
using System.Web.Http;
using System.Linq;
using System.Net.Http;
using ICSharpCode.SharpZipLib.Zip;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System;
using System.Threading;
using System.Net.Http.Headers;

public class DocumentsController : ApiController
{
    [Authorize]
    public DocumentUI GetDocument([FromUri] DocumentsFilter filter)
    {
        ProfileCommon profile = APIhelper.GetProfileCommon(Request);
        int companyId = DB.GetBorrowerClientCompanyId(profile, filter.BorrowerId);

        return DocumentUI.GetDocument(companyId, filter.DocumentId);
    }
    [Authorize]
    public IEnumerable<DocumentUI> GetDocumentsByCompanyUI([FromUri] DocumentsFilter filter)
    {
        return this.GetDocumentsByCompany(filter);
    }

    [Authorize]
    public IEnumerable<string> GetDocumentsTypes([FromUri] DocumentsFilter filter)
    {
        return this.GetDocumentsByCompanyUI(filter)
            .OrderBy(o => o.DocType)
            .Select(d => d.DocType)
            .Distinct();

    }

    [Authorize]
    public IHttpActionResult GetDocumentsZip([FromUri] DocumentsFilter filter)
    {
        if (String.IsNullOrEmpty( filter.FileType ) )
        {
            return null;
        }

        IEnumerable<DocumentUI> docs = this.GetDocumentsByCompany(filter);
        IEnumerable<DocumentUI> filterDocs = docs;
        if (!filter.All)
        {
            filterDocs = docs.Where(
                d => d.SyncDate >= filter.From
                && d.SyncDate <= filter.To);
        }       

        string guid = Guid.NewGuid().ToString();
        string workingFolder = Util.GetAppSettings("WorkingFolder");
        string tempFolder = Path.Combine(workingFolder, guid) + @"\";
        string syncRoot = Util.GetAppSettings("DocRoot");

        Directory.CreateDirectory(tempFolder);

        filterDocs.ToList().ForEach(d => {
            string source = syncRoot + d.Path;

            if (File.Exists(source))
            {
                try
                {
                    int tryCount = 1;
                    string fname = Path.GetFileName(d.Path);
                    if (fname.StartsWith("Q") && fname.Length > 16)
                        fname = fname.Substring(13);
                    // find a unique name
                    string newPath = tempFolder + fname;

                    while (File.Exists(newPath))
                    {
                        newPath = tempFolder 
                            + Path.GetFileNameWithoutExtension(fname) 
                            + " (" + (tryCount++).ToString() + ")" + Path.GetExtension(fname);
                    }

                    this.CopyFile(source, newPath);
                    
                }
                catch { }
            }
            
        });

        FastZip fastZip = new FastZip();
        fastZip.CreateEmptyDirectories = true;
        fastZip.RestoreAttributesOnExtract = true;
        fastZip.RestoreDateTimeOnExtract = true;

        string pathToZip = tempFolder.TrimEnd(new char[] { '\\' }) + ".zip";

        fastZip.CreateZip(pathToZip, tempFolder, false, null);

        return  new FileActionResult(pathToZip);
    }

    [Authorize]
    private IEnumerable<DocumentUI> GetDocumentsByCompany(DocumentsFilter filter)
    {
        ProfileCommon profile = APIhelper.GetProfileCommon(Request);
        web_Company wc = DB.GetCompanyByID(profile.CompanyID);

        return DocumentUI.GetDocumentsByCompanyUI(profile,
            wc,
            filter.BorrowerId,
            filter.FileType);

    }

    [Authorize]
    public IEnumerable<string> GetDocumentConfigs(int id)
    {
        ProfileCommon profile = APIhelper.GetProfileCommon(Request);

        if (Security.IsAdmin(profile) || Security.IsSuperAdmin(profile))
        {
            return DB.GetDocumentConfigsByCompanyId(id);

        } else
            return new List<string>();


    }

    private void CopyFile(string sourcePath, string destinationPath)
    {
        using (Stream source = File.Open(sourcePath, FileMode.Open))
        {
            using (Stream destination = File.Create(destinationPath))
            {
                source.CopyTo(destination);
            }
        }
    }

    private web_Document GetWebDocument(string id)
    {
        web_Document doc = null;
        ProfileCommon profile = APIhelper.GetProfileCommon(Request);
        using (PolicyDataSourceDataContext db = new PolicyDataSourceDataContext())
        {
            db.ObjectTrackingEnabled = false;
            doc = (from d in db.web_Documents
                   where d.DocumentID == id
                       && (d.CompanyID == profile.CompanyID)
                   select d).FirstOrDefault();
        }

        return doc;
    }

    [Authorize]
    [HttpGet]
    public HttpResponseMessage Download(string documentID)
    {
        Log.LogSubType subType = Log.LogSubType.DocumentIDInvalid;
        ProfileCommon profile = APIhelper.GetProfileCommon(Request);
        bool error = true;
        if (!string.IsNullOrEmpty(documentID))
        {
            web_Document doc = GetWebDocument(documentID);

            if (doc != null)
            {
                string file = Path.Combine(Util.GetAppSettings("DocRoot") + doc.Path);
                
                if (File.Exists(file))

                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK);
                    var stream = new FileStream(file, FileMode.Open);
                    var fileInfo = new FileInfo(file);
                    response.Content = new StreamContent(stream);
                    response.Content.Headers.ContentLength = stream.Length;
                    response.Content.Headers.ContentType = new MediaTypeHeaderValue(Util.GetMimeTypeFromExtension(fileInfo.Extension));                    
                    response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = fileInfo.Name };

                    return response;
                }
                else
                {
                    subType = Log.LogSubType.DocumentNotFound;
                    Log.WriteLog(Log.LogType.Exception, User.Identity.Name, subType, documentID, file, null);                    
                }
            }
            else
            {
                subType = Log.LogSubType.DocumentRecordMissing;
                Log.WriteLog(Log.LogType.Exception, profile.UserName, subType, documentID, null, null);                
            }
        }

        if (error)
        {
            Log.WriteLog(Log.LogType.Exception, profile.UserName, subType, documentID, null, null);
        }

        return new HttpResponseMessage(HttpStatusCode.OK);
    }


    /* Download PDF*/
    [Authorize]
    public IHttpActionResult GetDownload(string id)
    {

        IHttpActionResult result = null;
        ProfileCommon profile = APIhelper.GetProfileCommon(Request);

        if (!string.IsNullOrEmpty(id))
        {
            web_Document doc = GetWebDocument(id);

            if (doc != null)
            {
                string file = Path.Combine( Util.GetAppSettings("DocRoot") + doc.Path);

                var fileinfo = new FileInfo(file);
                try
                {
                    if (!fileinfo.Exists)
                    {
                        throw new FileNotFoundException(fileinfo.Name);
                    }

                    result = new FileActionResult(file);
                    
                }
                catch (Exception ex)
                {
                    result = ResponseMessage(Request.CreateErrorResponse(HttpStatusCode.NotFound, "File not found"));
                }

            }
        }

        return result;
    }    
}