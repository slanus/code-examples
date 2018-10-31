using System.Collections.Generic;
using System.Web.Http;

public class PremiumController : ApiController
{
    [Authorize]
    public IEnumerable<PremiumListUI> GetByPolicyUI([FromUri] PoliciesFilter filter)
    {
        ProfileCommon profile = APIhelper.GetProfileCommon(Request);

        return PremiumListUI.GetPremiumsByPolicyUI(profile, filter);

    }

    [Authorize]
    public IEnumerable<PremiumListUI> GetPremiumsByCompanyUI([FromUri] string id)
    {
        ProfileCommon profile = APIhelper.GetProfileCommon(Request);
        int companyId = DB.GetBorrowerClientCompanyId(profile, id);

        return PremiumListUI.GetPremiumsByCompanyUI(companyId, profile, id);
    }

    [Authorize]
    public IEnumerable<CashValueUI> GetCashValuesByPolicy([FromUri] PoliciesFilter filter)
    {
        ProfileCommon profile = APIhelper.GetProfileCommon(Request);
        web_Company wc = DB.GetCompanyByID(profile.CompanyID);

        return CashValueUI.GetCashValuesByPolicy(profile, wc, filter);
    }
}
