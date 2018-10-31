using Atlas.FactElec.Core.DTO;
using Atlas.FactElec.Core.Enum;
using Atlas.FactElec.Services;
using Microsoft.Web.Http;
using System.Collections.Generic;
using System.Net.Http.Formatting;
using System.Threading.Tasks;
using System.Web.Http;

namespace Atlas.FactElec.WebAPI.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/OrdersWithoutCae")]
    [Authorize(Roles = "Admin, Supervisor, Usuario")]
    public class OrdersWithoutCaeController : BaseOrdersController
    {
        private readonly OrdersWithoutCaeService _ordersService;
        private readonly AuthRepository _authRepository;
        private readonly LinesService _linesService;
        private readonly HeaderService _headerService;
        
        public OrdersWithoutCaeController(OrdersWithoutCaeService ordersService, LinesService linesService, HeaderService headerService, AuthRepository authRepository)
        {
            _ordersService = ordersService;
            _authRepository = authRepository;
            _linesService = linesService;
            _headerService = headerService;
        }

        public DataTableDTO<OrderHeaderDTO> Post(FormDataCollection form)
        {
            var dtFilters = GetFilters(form, FilterType.Orders);
            var salesOrders = _ordersService.List(dtFilters);

            return DataTableService.Sort(GetDataTableData(form), salesOrders);
        }

        [Route("api/v{version:apiVersion}/OrdersWithoutCae/GetDetailsHeader")]
        public OrderHeaderDTO GetDetailsHeader(string orderNumber)
        {
            return _headerService.GetHeader(orderNumber, FilterType.Orders);
        }

        [HttpGet]
        [Route("api/v{version:apiVersion}/OrdersWithoutCae/GetDetailsLines")]     
        public IEnumerable<OrderLineDTO> GetDetailsLines(string orderNumber, string deliveryNoteNumber, string orderType, string receiptLetter)
        {
            return (receiptLetter != "B")
                        ? _linesService.GetLines(orderNumber, deliveryNoteNumber, orderType)
                        : _linesService.GetLinesForInvoicesTypeB(orderNumber, deliveryNoteNumber, orderType);
        }

        [HttpGet]
        [Route("api/v{version:apiVersion}/OrdersWithoutCae/GetEquipmentsLines")]
        public IEnumerable<EquipmentDTO> GeDetailLinesCS(string orderNumber)
        {
            return _linesService.GetEquipmentsLines(orderNumber);
        }

        [HttpPost]
        [Route("api/v{version:apiVersion}/OrdersWithoutCae/RequestCAE")]
        public IHttpActionResult RequestCAE([FromBody] RequestCaeDTO dto)
        {
            // Get user data
            var user = GetCurrentUser();
            dto.UserId = user.Id;
            dto.UserEmail = user.Email;
            dto.UserFullName = string.Format("{0} {1}", user.Name, user.LastName);
            
            // Request CAE number
            var result = _ordersService.RequestCae(dto);
            return Json(result);
        }

        [HttpGet]
        [Route("api/v{version:apiVersion}/OrdersWithoutCae/GetAfipValidations")]
        public IHttpActionResult GetAfipValidations(string orderNumber, string clientCode, string deliveryCode)
        {
            var result = _ordersService.GetAfipValidations(orderNumber, clientCode, deliveryCode);
            return Json(result);
        }

        [HttpGet]
        [Route("api/v{version:apiVersion}/OrdersWithoutCae/GetDetailsTaxes")]
        public IHttpActionResult GetDetailsTaxes(string orderNumber, string deliveryNoteNumber, string orderType)
        {
            var result = _ordersService.GetDetailsTaxes(orderNumber, deliveryNoteNumber, orderType, FilterType.Orders);
            return Json(result);
        }

        [HttpGet]
        [Route("api/v{version:apiVersion}/OrdersWithoutCae/ListPLCs")]
        public IHttpActionResult ListPLCs()
        {
            var result = _ordersService.ListPLCs();
            return Json(result);
        }

        [HttpGet]
        [Route("api/v{version:apiVersion}/OrdersWithoutCae/ListPointOfSales")]
        public IHttpActionResult ListPointOfSales()
        {
            var result = _ordersService.ListPointOfSales();
            return Json(result);
        }
    }
}