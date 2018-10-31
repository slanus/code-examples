using Atlas.FactElec.Core.DTO;
using Atlas.FactElec.Data.ADO;
using Atlas.FactElec.Data.EntityFramework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Atlas.FactElec.Services
{
    /// <summary>
    /// Client Service
    /// </summary>
    public class ClientService : BusinessService
    {
        /// <summary>
        /// Service constructor
        /// </summary>
        /// <param name="scalaDataContext">ScalaDataContext</param>
        public ClientService(ScalaDataContext scalaDataContext, AtlasDataContext atlasDataContext)
        {
            ScalaDataContext = scalaDataContext;
            AtlasDataContext = atlasDataContext; 
        }

        /// <summary>
        /// Search a client by name
        /// </summary>
        /// <param name="clientName">Name of the client</param>
        /// <returns>A list with the first 15 results</returns>
        /// <remarks>This methods only work if the string contains more than three characters</remarks>
        public IEnumerable<ClientSearchResultDTO> SearchClient(string clientName)
        {
            return (clientName.Length > 3) 
                ? ScalaDataContext.SP_BuscarCliente(clientName) 
                : new List<ClientSearchResultDTO>();
        }

        /// <summary>
        /// List all emails from a client (it merges all emails saved in Scala with the emails saved in our database)
        /// </summary>
        /// <param name="clientId">Client Id</param>
        /// <returns>A list with all the client emails</returns>
        public IEnumerable<String> ListEmails(string clientId)
        {
            var lst = new List<string>();
            lst.AddRange(ScalaDataContext.SP_ObtenerEmailsPorCliente(clientId));
            lst.AddRange(AtlasDataContext.ClientEmails.Where(x => x.ClientId == clientId).Select(y => y.Email).ToList()); 

            return lst.Distinct();
        }

        /// <summary>
        /// List all delivery addresses
        /// </summary>
        /// <param name="clientId">Client Id</param>
        /// <param name="orderNumber">Order Number</param>
        /// <param name="orderType">Order type</param>
        /// <returns>A list with all the client delivery addresses</returns>
        public IEnumerable<DeliveryAddressDTO> ListDeliveryAddresses(string clientId, string orderNumber, string orderType)
        {
            return ScalaDataContext.SP_ListarDireccionesDeEntrega(clientId, orderNumber, orderType)
                    .Where(x => !string.IsNullOrWhiteSpace(x.Address))
                    .Distinct()
                    .ToList();
        }

        /// <summary>
        /// List all taxliability codes with their descriptions
        /// </summary>
        /// <returns>A list of TaxliabilityDTO</returns>
        public IEnumerable<TaxliabilityDTO> ListTaxliabilityCodes()
        {            
            return ScalaDataContext.SP_ResponsabilidadFiscal();
        }

        /// <summary>
        /// Add a new email for the client
        /// </summary>
        /// <param name="email">Email client</param>
        /// <param name="clientId">Client Id</param>
        public void AddEmail(string email, string clientId)
        {
            if (!string.IsNullOrWhiteSpace(email) && AtlasDataContext.ClientEmails.Where(x => x.ClientId == clientId && x.Email == email.Trim()).Count() == 0)
            {
                var clientEmail = new ClientEmail
                {
                    Email = email.Trim(),
                    ClientId = clientId
                };

                AtlasDataContext.ClientEmails.Add(clientEmail);
                AtlasDataContext.SaveChanges();
            }
        }
    }
}
