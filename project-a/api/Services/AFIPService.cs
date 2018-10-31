using Atlas.FactElec.AFIP;
using Atlas.FactElec.AFIP.Wsfev1;
using Atlas.FactElec.AFIP.Wsfexv1;
using Atlas.FactElec.Core.DTO;
using Atlas.FactElec.Core.Enum;
using Atlas.FactElec.Data.ADO;
using Atlas.FactElec.Data.EntityFramework;
using Atlas.FactElec.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Data.Entity.Validation;
using System.Globalization;
using System.Linq;

namespace Atlas.FactElec.Services
{
    public class AFIPService : BusinessService
    {
        private readonly TaxesService _taxesService;
        private readonly ReportService _reportService;
        private readonly LinesService _linesService;
        private readonly HeaderService _headerService;

        /// <summary>
        /// Afip Service Constructor
        /// </summary>
        /// <param name="scalaDataContext">ScalaDataContext</param>
        /// <param name="atlasDataContext">AtlasDataContext</param>
        public AFIPService(ScalaDataContext scalaDataContext, AtlasDataContext atlasDataContext, TaxesService taxesService, ReportService reportService, LinesService linesService, HeaderService headerService)
        {
            ScalaDataContext = scalaDataContext;
            AtlasDataContext = atlasDataContext;
            _taxesService = taxesService;
            _reportService = reportService;
            _linesService = linesService;
            _headerService = headerService;
        }
        
        /// <summary>
        /// Determines if it's possible to request another CAE for that point of sale
        /// </summary>
        /// <param name="pointOfSale">Point of sale to validate</param>
        /// <param name="receiptType">Receipt type</param>
        /// <param name="receiptLetter">Receipt letter</param>
        /// <param name="orderNumber">Order Number</param>
        /// <param name="deliveryNoteNumber">Delivery note number</param>
        /// <returns>True if it's possible to generate another. Fakse otherwise.</returns>
        public bool CanRequestCae(string pointOfSale, string receiptType, string receiptLetter, string orderNumber, string deliveryNoteNumber)
        {
            var type = Convert.ToInt32(receiptType);
            deliveryNoteNumber = (deliveryNoteNumber == null) ? "" : deliveryNoteNumber;

            var validInvoiceStatus = AtlasDataContext.Invoices
                        .Where(x => x.InvoiceNumber.Substring(1, 5) == pointOfSale.Trim()
                                    && x.InvoiceType == type
                                    && x.InvoiceLetter == receiptLetter.Trim()
                                    && x.Status == (int)OrderStatus.Pending)
                        .Count() == 0;

            var validOrderStatus = AtlasDataContext.Orders
                        .Where(x => x.PointOfSale == pointOfSale.Trim()
                                    && x.InvoiceType == type
                                    && x.InvoiceLetter == receiptLetter.Trim()
                                    && x.ReportSaved == false
                                    && x.Status == "A")
                        .Count() == 0;

            var validOrder = AtlasDataContext.Orders
                        .Where(x => x.OrderNumber == orderNumber
                                    && x.DeliveryNote == deliveryNoteNumber
                                    && x.ReportSaved == false
                                    && x.Status == "A")
                        .Count() == 0;

            // We need to verify in two different places, because an order can get a CAE number 
            // and then fail to save invoice data into the invoices table. In that case, we must 
            // be sure the user can't create more orders for the same point of sale, invoice letter and invoice type.
            return validInvoiceStatus && validOrderStatus && validOrder;
        }
        
        /// <summary>
        /// Determines if an invoice already exists in our database
        /// </summary>
        /// <param name="invoiceLetter">Invoice Letter</param>
        /// <param name="pointOfSale">Point of Sale</param>
        /// <param name="invoiceNumber">Invoice Number</param>
        /// <param name="invoiceType">Invoice Type</param>
        /// <returns>Determines if the invoice number is valid to generate another receipt</returns>
        public bool ValidInvoiceNumber(string invoiceLetter, string pointOfSale, int invoiceNumber, int invoiceType)
        {
            // Empty database
            var cleanDatabase = AtlasDataContext.Invoices.Count() == 0;

            // Valid last receipt number
            var lasttReceiptNumber = GetInvoiceNumber(invoiceLetter, pointOfSale, invoiceNumber.ToString());
            var validLastNumber = AtlasDataContext.Invoices.Where(x => x.InvoiceNumber == lasttReceiptNumber && x.InvoiceType == invoiceType).Count() == 1;

            // Valid next receipt number
            var nextReceiptNumber = GetInvoiceNumber(invoiceLetter, pointOfSale, (invoiceNumber + 1).ToString());
            var validNextNumber = AtlasDataContext.Invoices.Where(x => x.InvoiceNumber == nextReceiptNumber && x.InvoiceType == invoiceType).Count() == 0;
            
            return cleanDatabase || (validLastNumber && validNextNumber);
        }

        /// <summary>
        /// Request CAE from AFIP
        /// </summary>        
        /// <param name="orderNumber">Order Number</param>
        /// <param name="clientCode">Client Code</param>
        /// <param name="deliveryNote">Order Number</param>
        /// <param name="userId">UserId</param>
        /// <returns>AFIP Result</returns>
        public CAEResultDTO RequestCAE(RequestCaeDTO dto)
        {
            // Get order data
            var filter = new OrderFilterDTO();
            var orderData = _headerService.GetHeader(dto.OrderNumber, FilterType.Orders);

            if (orderData == null)
            {
                throw new ArgumentException(
                    string.Format("Data not found for the client {0} with order number number {1}", 
                    dto.ClientCode, dto.OrderNumber));
            }

            // Validate receipt type, letter and point of sale
            if(string.IsNullOrWhiteSpace(orderData.ReceiptType))
                return NotValidResult(dto, ServicesResources.VALIDATION_RECEIPT_TYPE);

            if (string.IsNullOrWhiteSpace(orderData.ReceiptLetter))
                return NotValidResult(dto, ServicesResources.VALIDATION_RECEIPT_LETTER);

            if (string.IsNullOrWhiteSpace(orderData.PointOfSale))
                return NotValidResult(dto, ServicesResources.VALIDATION_POINT_OF_SALE);

            if (orderData.AccountingDimension == "0")
                return NotValidResult(dto, ServicesResources.VALIDATION_ACCOUNTING_DIMENSION);
            
            // Get client data
            var clientData = ScalaDataContext.SP_ComprobantesDatosCliente(dto.ClientCode, dto.OrderNumber, orderData.OrderType);
            if (clientData == null)
            {
                throw new ArgumentException(
                    string.Format("Data not found for client {0} with order number {1}",
                    dto.ClientCode, dto.OrderNumber));
            }

            // Get lines 
            var lines = _linesService.GetLines(dto.OrderNumber, dto.DeliveryNote, orderData.OrderType).ToList();
            if (lines == null || lines.Count == 0)
                return NotValidResult(dto, ServicesResources.VALIDATION_NO_LINES);

            // Check if all lines have a valid quantity
            if (lines.Where(x => x.Quantity == 0).Any())
                return NotValidResult(dto, ServicesResources.VALIDATION_NO_QUANTITY);

            // Check lines sign
            foreach (var line in lines)
            {
                if (orderData.ReceiptType != "2")
                {
                    if (line.UnitPrice < 0 && line.Quantity < 0)
                        return NotValidResult(dto, ServicesResources.VALIDATION_LINES_SIGN);
                }
                else
                {
                    if (line.UnitPrice < 0 && line.Quantity < 0)
                        return NotValidResult(dto, ServicesResources.VALIDATION_LINES_SIGN);

                    if (!orderData.ValidLinesSign)
                        return NotValidResult(dto, ServicesResources.VALIDATION_LINES_FIXED_PRICE_SIGN);
                }
            }

            // Get taxes
            var taxes = _taxesService.Calculate(orderData, lines);

            // Check if the receipt type is valid
            if (taxes.TaxBase < 0 && orderData.ReceiptType != "2")
                return NotValidResult(dto, ServicesResources.VALIDATION_RECEIPT_TYPE_MISMATCH);

            if (taxes.TaxBase > 0 && orderData.ReceiptType == "2")
                return NotValidResult(dto, ServicesResources.VALIDATION_RECEIPT_TYPE_MISMATCH);

            // Check amount
            if(taxes.Total == 0)
                return NotValidResult(dto, ServicesResources.VALIDATION_AMOUNT);

            // Check that only taxed orders can contain tributes
            if ((taxes.NotTaxedTotal > 0 || taxes.ExemptTotal > 0) && taxes.TributesTotal > 0)
                return NotValidResult(dto, ServicesResources.VALIDATION_NONTAXED_OR_EXEMPT_TRIBUTES);

            // Get Equipments (only for CS)
            var equipments = new List<EquipmentDTO>();
            if (dto.OrderType == "CS")
            {
                equipments.AddRange(ScalaDataContext.SP_ComprobantesLineas_CS_detalle(dto.OrderNumber));

                // Validate if the equipments contain a valid accounting dimension
                foreach (var eqp in equipments)
                {
                    if(eqp.AccountingDimension == "0")
                        return NotValidResult(dto, ServicesResources.VALIDATION_EQUIPMENT_ACCOUNTING_DIMENSION);
                }
            }

            // Check if it's possible to request a CAE Number
            if (!CanRequestCae(orderData.PointOfSale, orderData.ReceiptType, orderData.ReceiptLetter, orderData.OrderNumber, orderData.DeliveryNoteNumber))
                return NotValidResult(dto, ServicesResources.VALIDATION_PENDING_CLOSURE);

            // Get CAE based on the invoice letter (we must call a different service for export invoices)
            return (orderData.ReceiptLetter != "E")
                    ? RequestFeCAE(dto, orderData, clientData, lines, equipments, taxes)
                    : RequestFexCAE(dto, orderData, clientData, lines, equipments, taxes);
        }

        /// <summary>
        /// Returns a not valid result
        /// </summary>
        /// <param name="dto">RequestCaeDTO</param>
        /// <param name="validationMessage">Validation Message</param>
        /// <returns></returns>
        private CAEResultDTO NotValidResult(RequestCaeDTO dto, string validationMessage)
        {
            return new CAEResultDTO()
            {
                OrderNumber = dto.OrderNumber,
                ClientCode = dto.ClientCode,
                DeliveryNote = dto.DeliveryNote,
                Result = "E",
                ValidationMessage = validationMessage
            };
        }

        /// <summary>
        /// Request CAE for export invoices
        /// </summary>
        /// <param name="dto">DTO</param>
        /// <param name="orderData">Order data</param>
        /// <param name="clientData">Client data</param>
        /// <param name="lines">Lines data</param>
        /// <param name="equipments">Equipments</param>
        /// <param name="taxes">Taxes</param>
        /// <returns></returns>
        private CAEResultDTO RequestFexCAE(RequestCaeDTO dto, OrderHeaderDTO orderData, OrderClientDataDTO clientData, List<OrderLineDTO> lines, List<EquipmentDTO> equipments, AfipTaxResultDTO taxes)
        {
            FEXResponseAuthorize result = null;
            var emailData = new EmailDataDTO();
            var reportUrl = "";

            try
            {
                var afipConnector = new WSFEXConnector();
                var receptType = Convert.ToInt16(GetAfipReceiptType(orderData.ReceiptLetter, orderData.ReceiptType));
                var exportType = Convert.ToInt16(dto.ExportTypeId);
                var exportPermit = Convert.ToInt16(dto.ExportPermitId);
                var nextReceiptNumber = afipConnector.ObtenerUltimoComprobanteAutorizado(Convert.ToInt16(orderData.PointOfSale), receptType);
                var nextId = afipConnector.ObtenerUltimoID();

                // TODO: Validate nextReceiptNumber and nextId errors. 
                var lastReceiptNumber = Convert.ToInt32(nextReceiptNumber.FEXResult_LastCMP.Cbte_nro);

                // We need to validate the previous invoice just to be sure that it was succesfully created
                //if (ValidInvoiceNumber(orderData.ReceiptLetter, orderData.PointOfSale, lastReceiptNumber, Convert.ToInt32(orderData.ReceiptType)))
                //    return NotValidResult(dto, ServicesResources.VALIDATION_RECEIPT_NUMBER);

                // Set Request
                var request = new ClsFEXRequest();
                request.Id = nextId.FEXResultGet.Id + 1;
                request.Tipo_cbte = receptType;
                request.Fecha_cbte = dto.Date.ToString("yyyyMMdd");
                request.Punto_vta = Convert.ToInt16(orderData.PointOfSale);
                request.Cbte_nro = nextReceiptNumber.FEXResult_LastCMP.Cbte_nro + 1;
                request.Tipo_expo = exportType;
                request.Permiso_existente = GetAfipPermission(receptType, exportType, exportPermit);
                request.Dst_cmp = GetAfipCountryId(clientData.CountryCode);
                request.Cliente = clientData.BusinessName;
                request.Cuit_pais_cliente = Convert.ToInt64(clientData.CountryCUIT.Replace("-", ""));
                request.Moneda_Id = GetAfipCurrencyId(orderData.CurrencyCode);
                request.Moneda_ctz = AfipHelper.FormatNumber(dto.ExchangeRate);
                request.Imp_total = AfipHelper.FormatNumber(taxes.Total);
                request.Idioma_cbte = Convert.ToInt16(AfipInvoiceLanguage.Spanish);
                request.Domicilio_cliente = clientData.ClientAddress;
                
                if (!string.IsNullOrEmpty(orderData.TermsOfDelivery) && orderData.TermsOfDelivery.Length >= 3)
                    request.Incoterms = orderData.TermsOfDelivery.Substring(0, 3);

                var lineIndex = 0;
                request.Items = new Item[lines.Count];
                foreach (var line in lines)
                {
                    var item = new Item();
                    item.Pro_ds = line.Description;
                    item.Pro_total_item = Math.Abs(AfipHelper.FormatNumber(line.Amount));

                    var measurementUnit = Convert.ToInt32(line.MeasurementUnit);
                    if (measurementUnit != 0)
                    {
                        item.Pro_umed = GetAfipMeasurementUnit(measurementUnit);
                        item.Pro_precio_uni = AfipHelper.FormatNumber(line.UnitPrice);
                        item.Pro_qty = AfipHelper.FormatNumber(Math.Abs(line.Quantity));
                    }
                    else
                    {
                        item.Pro_precio_uni = 0;
                        item.Pro_qty = 0;
                    }

                    request.Items.SetValue(item, lineIndex);
                    lineIndex++;
                }

                // Log Afip Request
                var order = GetOrder(orderData);
                var req = LogAfipRequest(order, request);

                // Request CAE                
                result = afipConnector.SolicitarCAE(request, order.OrderNumber);

                // Log Afip Response
                LogAfipResponse(req, result);

                //// If we got a CAE number, we must save this order data as an invoice
                if (ValidResponse(result))
                {
                    var invoiceNumber = SaveInvoice(dto, orderData, clientData, lines, equipments, taxes, request, result);
                    _reportService.SaveReport(invoiceNumber, orderData.ReceiptType);
                    reportUrl = GetReportUrl(orderData.ReceiptType, invoiceNumber);
                    emailData = GetEmailData(invoiceNumber, dto.UserEmail, dto.UserFullName, orderData);

                    // Set report saved
                    order.ReportSaved = true;
                    AtlasDataContext.SaveChanges();
                }

            }
            catch (DbEntityValidationException dbEx)
            {
                Exception raise = dbEx;
                foreach (var validationErrors in dbEx.EntityValidationErrors)
                {
                    foreach (var validationError in validationErrors.ValidationErrors)
                    {
                        string message = string.Format("{0}:{1}",
                            validationErrors.Entry.Entity.ToString(),
                            validationError.ErrorMessage);
                        
                        // raise a new exception nesting
                        // the current instance as InnerException
                        raise = new InvalidOperationException(message, raise);
                    }
                }
                throw raise;
            }
            catch (Exception ex)
            {
                LogHelper.Log.Error(ex);
            }

            return ProcessResult(dto.OrderNumber, dto.ClientCode, dto.DeliveryNote, orderData.OrderType, dto.UserId, orderData.NextInvDate, reportUrl, emailData, result);
        }

        /// <summary>
        /// Get CAE for an invoice
        /// </summary>
        /// <param name="dto">RequestCAE DTO</param>
        /// <param name="orderData">Order data</param>
        /// <param name="clientData">Client data</param>
        /// <param name="lines">Order lines</param>
        /// <param name="equipments">Contract service equipments</param>
        /// <param name="taxes">Order taxes</param>
        /// <returns>A CAEResultDTO object with the response</returns>
        private CAEResultDTO RequestFeCAE(RequestCaeDTO dto, OrderHeaderDTO orderData, OrderClientDataDTO clientData, List<OrderLineDTO> lines, List<EquipmentDTO> equipments, AfipTaxResultDTO taxes)
        {
            FECAEResponse result = null;
            var emailData = new EmailDataDTO();
            var reportUrl = "";

            try
            {
                var afipReceptType = GetAfipReceiptType(orderData.ReceiptLetter, orderData.ReceiptType);

                // Set Header
                var cabRequest = new FECAECabRequest
                {
                    CantReg = 1,    // We send only one invoice
                    PtoVta = Convert.ToInt32(orderData.PointOfSale),
                    CbteTipo = afipReceptType
                };

                // Set Details
                var lastReceiptNumber = new WSFEConnector().ObtenerUltimoComprobanteAutorizado(Convert.ToInt32(orderData.PointOfSale), afipReceptType);
                var concepto = GetConcept(orderData);

                // We need to validate the previous invoice just to be sure that it was succesfully created
                //if (ValidInvoiceNumber(orderData.ReceiptLetter, orderData.PointOfSale, lastReceiptNumber.CbteNro, Convert.ToInt32( orderData.ReceiptType)))
                //    NotValidResult(dto, ServicesResources.VALIDATION_RECEIPT_NUMBER);

                var details = new FECAEDetRequest();
                details.Concepto = concepto;
                details.DocTipo = Convert.ToInt32(ConfigurationManager.AppSettings.Get("afipCuitCode"));
                details.DocNro = Convert.ToInt64(clientData.CUIT.Replace("-", ""));
                details.CbteDesde = lastReceiptNumber.CbteNro + 1;
                details.CbteHasta = lastReceiptNumber.CbteNro + 1;
                details.CbteFch = dto.Date.ToString("yyyyMMdd");
                details.ImpTotal = Math.Abs(AfipHelper.FormatNumber(taxes.Total));
                details.ImpNeto = Math.Abs(AfipHelper.FormatNumber(taxes.NetAmountTotal));
                details.ImpTotConc = Math.Abs(AfipHelper.FormatNumber(taxes.NotTaxedTotal));
                details.ImpOpEx = Math.Abs(AfipHelper.FormatNumber(taxes.ExemptTotal));
                details.ImpTrib = Math.Abs(AfipHelper.FormatNumber(taxes.TributesTotal));
                details.ImpIVA = Math.Abs(AfipHelper.FormatNumber(taxes.IvaTotal));
                details.MonId = GetAfipCurrencyId(orderData.CurrencyCode);
                details.MonCotiz = Convert.ToDouble(dto.ExchangeRate);

                if (concepto == 2 || concepto == 3)
                {
                    details.FchServDesde = orderData.ContractFromDate.ToString("yyyyMMdd");
                    details.FchServHasta = orderData.ContractToDate.ToString("yyyyMMdd");
                    details.FchVtoPago = GetInvoiceDueDate(orderData.InvoiceExpirationDays, dto.Date).ToString("yyyyMMdd");
                }

                if (taxes.Tributes.Count > 0)
                {
                    details.Tributos = new Tributo[taxes.Tributes.Count];
                    var tributesIndex = 0;
                    foreach (var tax in taxes.Tributes)
                    {
                        var tributo = new Tributo();
                        tributo.Id = GetAfipTributeId(tax.Type);
                        tributo.BaseImp = Math.Abs(AfipHelper.FormatNumber(tax.TaxBase));
                        tributo.Alic = AfipHelper.FormatNumber(tax.Aliquot);
                        tributo.Importe = Math.Abs(AfipHelper.FormatNumber(tax.Amount));

                        if (tributo.Id == 99)
                            tributo.Desc = tax.Description;

                        details.Tributos.SetValue(tributo, tributesIndex);
                        tributesIndex++;
                    }
                }

                if (details.ImpIVA > 0)
                {
                    var ivaIndex = 0;
                    details.Iva = new AlicIva[taxes.IVAs.Count];
                    foreach (var tax in taxes.IVAs)
                    {
                        var iva = new AlicIva();
                        iva.Id = GetAfipIVAId("IVA", tax.Aliquot * 0.01M);
                        iva.BaseImp = Math.Abs(AfipHelper.FormatNumber(tax.TaxBase));
                        iva.Importe = Math.Abs(AfipHelper.FormatNumber(tax.TotalTaxAmount));

                        details.Iva.SetValue(iva, ivaIndex);
                        ivaIndex++;
                    }
                }

                // Prepare fecaeRequest
                var fecaeRequest = new FECAERequest
                {
                    FeCabReq = cabRequest,
                    FeDetReq = new FECAEDetRequest[1]
                };

                fecaeRequest.FeDetReq.SetValue(details, 0);

                // Log Afip Request
                var order = GetOrder(orderData);
                var request = LogAfipRequest(order, fecaeRequest);

                // Request CAE
                var afipConnector = new WSFEConnector();
                result = afipConnector.SolicitaCAE(fecaeRequest, order.OrderNumber);

                // Log Afip Response
                LogAfipResponse(request, result);

                // If we got a CAE number, we must save this order data as an invoice
                if (ValidResponse(result))
                {
                    // If it's a type B invoice, then we must save the lines with the IVA amount calculated
                    if (orderData.ReceiptLetter == "B")
                        lines = _linesService.GetLinesForInvoicesTypeB(orderData.OrderNumber, orderData.DeliveryNoteNumber, orderData.OrderType).ToList();

                    // Generate report and save invoice
                    var invoiceNumber = SaveInvoice(dto, orderData, clientData, lines, equipments, taxes, fecaeRequest, result);
                    _reportService.SaveReport(invoiceNumber, orderData.ReceiptType);
                    reportUrl = GetReportUrl(orderData.ReceiptType, invoiceNumber);
                    emailData = GetEmailData(invoiceNumber, dto.UserEmail, dto.UserFullName, orderData);

                    // Set report saved
                    order.ReportSaved = true;
                    AtlasDataContext.SaveChanges();
                }
            }
            catch (DbEntityValidationException dbEx)
            {
                Exception raise = dbEx;
                foreach (var validationErrors in dbEx.EntityValidationErrors)
                {
                    foreach (var validationError in validationErrors.ValidationErrors)
                    {
                        string message = string.Format("{0}:{1}",
                            validationErrors.Entry.Entity.ToString(),
                            validationError.ErrorMessage);

                        // raise a new exception nesting
                        // the current instance as InnerException
                        raise = new InvalidOperationException(message, raise);
                    }
                }
                throw raise;
            }
            catch (Exception ex)
            {
                LogHelper.Log.Error(ex);
            }

            // Process AFIP Result
            return ProcessResult(dto.OrderNumber, dto.ClientCode, dto.DeliveryNote, orderData.OrderType, dto.UserId, orderData.NextInvDate, reportUrl, emailData, result);
        }

        /// <summary>        
        /// Process FECAEResponse reponse (for common invoices)
        /// </summary>
        /// <param name="orderNumber">Order Number</param>
        /// <param name="clientCode">Client Code</param>
        /// <param name="deliveryNote">Delivery Note</param>
        /// <param name="orderType">Order Type</param>
        /// <param name="userId">UserId</param>
        /// <param name="reportUrl">Report URL</param>
        /// <param name="emailData">Email data</param>
        /// <param name="response">Afip Response</param>        
        /// <returns>A DTO that contains AFIP result</returns>
        private CAEResultDTO ProcessResult(string orderNumber, string clientCode, string deliveryNote, string orderType, string userId, DateTime nextInvDate, string reportUrl, EmailDataDTO emailData, FECAEResponse response)
        {
            var resultDTO = new CAEResultDTO
            {
                OrderNumber = orderNumber,
                OrderType = orderType,
                ClientCode = clientCode,
                DeliveryNote = deliveryNote,
                UserId = userId,
                ReportUrl = reportUrl,
                EmailData = emailData,
                LastInvoiceDate = nextInvDate,
                Result = "R"
            };

            if (response != null)
            {
                if (response.FeCabResp != null)
                {
                    resultDTO.Result = response.FeCabResp.Resultado;
                    resultDTO.Reprocessed = response.FeCabResp.Reproceso;
                }

                if (response.FeDetResp != null && response.FeDetResp.Any())
                {
                    resultDTO.CAE = response.FeDetResp.First().CAE;
                    resultDTO.DueDate = response.FeDetResp.First().CAEFchVto;
                }
            }

            return resultDTO;
        }

        /// <summary>
        /// Process FEXResponseAuthorize reponse (for export invoices)
        /// </summary>
        /// <param name="orderNumber">Order Number</param>
        /// <param name="clientCode">Client Code</param>
        /// <param name="deliveryNote">Delivery Note</param>
        /// <param name="orderType">Order type</param>
        /// <param name="userId">Current User Id</param>
        /// <param name="reportUrl">Invoice Number</param>
        /// <param name="emailData">Email Data</param>
        /// <param name="response">Afip Respone</param>
        /// <returns>A DTO that contains AFIP result</returns>
        private CAEResultDTO ProcessResult(string orderNumber, string clientCode, string deliveryNote, string orderType, string userId, DateTime nextInvDate, string reportUrl, EmailDataDTO emailData, FEXResponseAuthorize response)
        {
            var resultDTO = new CAEResultDTO
            {
                OrderNumber = orderNumber,
                OrderType = orderType,
                ClientCode = clientCode,
                DeliveryNote = deliveryNote,
                UserId = userId,
                ReportUrl = reportUrl,
                EmailData = emailData,
                LastInvoiceDate = nextInvDate,
                Result = "R"
            };

            if (response != null)
            {
                if (response.FEXResultAuth != null)
                {
                    resultDTO.Result = response.FEXResultAuth.Resultado;
                    resultDTO.Reprocessed = response.FEXResultAuth.Reproceso;
                    resultDTO.CAE = response.FEXResultAuth.Cae;
                    resultDTO.DueDate = response.FEXResultAuth.Fch_venc_Cae;
                }
            }

            return resultDTO;
        }

        /// <summary>
        /// Get info data to send by email
        /// </summary>
        /// <param name="invoiceNumber">Invoice Number</param>
        /// <param name="orderData">Order data</param>
        /// <returns></returns>
        private EmailDataDTO GetEmailData(string invoiceNumber, string userEmail, string userFullName, OrderHeaderDTO orderData)
        {
            var emailData = new EmailDataDTO
            {
                ClientCode = orderData.ClientCode,
                BussinessName = orderData.BusinessName,
                DeliveryNotes = orderData.DeliveryNotes,
                PurchaseOrder = orderData.PurchaseOrder,
                OrderDate = orderData.OrderDate,
                ReceiptType = orderData.ReceiptType,
                ReceiptNumber = invoiceNumber,
                UserFullName = userFullName,
                UserEmail = userEmail,
                EmailType = EmailType.ClientNotification
            };

            return emailData;
        }

        /// <summary>
        /// Log Afip Request into the database
        /// </summary>
        /// <param name="order">Order</param>
        /// <param name="fecaeRequest">FECAERequest object</param>
        /// <returns>Afip Request</returns>
        private AfipRequest LogAfipRequest(Order order, FECAERequest fecaeRequest)
        {
            var afipRequest = new AfipRequest
            {
                Date = DateTime.Now,
                RequestData = XMLHelper.ToXML(fecaeRequest)
            };

            order.AfipRequests.Add(afipRequest);

            if (order.Id == 0)
                AtlasDataContext.Orders.Add(order);

            AtlasDataContext.SaveChanges();
            return afipRequest;
        }

        /// <summary>
        /// Log Afip Request into the database
        /// </summary>
        /// <param name="order">Order</param>
        /// <param name="clsFEXRequest">ClsFEXRequest object</param>
        /// <returns>Afip Request</returns>
        private AfipRequest LogAfipRequest(Order order, ClsFEXRequest clsFEXRequest)
        {
            var afipRequest = new AfipRequest
            {
                Date = DateTime.Now,
                RequestData = XMLHelper.ToXML(clsFEXRequest)
            };

            order.AfipRequests.Add(afipRequest);

            if (order.Id == 0)
                AtlasDataContext.Orders.Add(order);

            AtlasDataContext.SaveChanges();
            return afipRequest;
        }

        /// <summary>
        /// Log AFIP Response into the database
        /// </summary>
        /// <param name="afipRequest">Afip Request</param>
        /// <param name="fecaeResponse">FECAEResponse object</param>
        /// <returns>Afip Response</returns>
        private AfipResponse LogAfipResponse(AfipRequest afipRequest, FECAEResponse fecaeResponse)
        {
            var errors = new List<AfipError>();
            var observations = new List<AfipObservation>();

            if (fecaeResponse != null && fecaeResponse.Errors != null)
                errors = fecaeResponse.Errors.Select(x => new AfipError() { Code = x.Code, Message = StringHelper.EncodeUTF8(x.Msg) }).ToList();

            if (fecaeResponse != null && fecaeResponse.FeDetResp != null)
            {
                foreach (var d in fecaeResponse.FeDetResp)
                {
                    if (d.Observaciones != null)
                        observations.AddRange(d.Observaciones.Select(x => new AfipObservation() { Code = x.Code, Message = StringHelper.EncodeUTF8(x.Msg) }));
                }

                // Update Order Status
                afipRequest.Order.Status = fecaeResponse.FeCabResp.Resultado;
                afipRequest.Order.Reprocessed = fecaeResponse.FeCabResp.Reproceso;
            }

            var afipResponse = new AfipResponse
            {
                Date = DateTime.Now,
                ResponseData = XMLHelper.ToXML(fecaeResponse),
                AfipErrors = errors,
                AfipObservations = observations,
            };

            afipRequest.AfipResponse = afipResponse;
            AtlasDataContext.SaveChanges();
            return afipResponse;
        }

        /// <summary>
        /// Log Afip response for export invoices into the database
        /// </summary>
        /// <param name="afipRequest">Afip Request</param>
        /// <param name="responseAuthorize">FEXResponseAuthorize object</param>
        /// <returns>Afip Response</returns>
        private AfipResponse LogAfipResponse(AfipRequest afipRequest, FEXResponseAuthorize responseAuthorize)
        {
            var errors = new List<AfipError>();
            var observations = new List<AfipObservation>();

            if (responseAuthorize != null && responseAuthorize.FEXErr != null)
                errors.Add(new AfipError() { Code = responseAuthorize.FEXErr.ErrCode, Message = StringHelper.EncodeUTF8(responseAuthorize.FEXErr.ErrMsg) });

            if (responseAuthorize != null && responseAuthorize.FEXEvents != null)
                observations.Add(new AfipObservation() { Code = responseAuthorize.FEXEvents.EventCode, Message = StringHelper.EncodeUTF8(responseAuthorize.FEXEvents.EventMsg) });

            if (responseAuthorize != null && responseAuthorize.FEXResultAuth != null)
            {
                // Update Order Status
                afipRequest.Order.Status = responseAuthorize.FEXResultAuth.Resultado;
                afipRequest.Order.Reprocessed = responseAuthorize.FEXResultAuth.Reproceso;
            }

            var afipResponse = new AfipResponse
            {
                Date = DateTime.Now,
                ResponseData = XMLHelper.ToXML(responseAuthorize),
                AfipErrors = errors,
                AfipObservations = observations,
            };

            afipRequest.AfipResponse = afipResponse;
            AtlasDataContext.SaveChanges();
            return afipResponse;
        }

        /// <summary>
        /// Get an order without CAE
        /// </summary>
        /// <param name="orderData">Order Header DTO</param>
        /// <returns>The order that needs to get a CAE number</returns>
        private Order GetOrder(OrderHeaderDTO orderData)
        {
            var order = AtlasDataContext.Orders.Where(x => x.OrderNumber == orderData.OrderNumber && x.ClientCode == orderData.ClientCode && x.DeliveryNote == orderData.DeliveryNoteNumber).FirstOrDefault();
            if (order == null)
            {
                return new Order
                {
                    OrderNumber = orderData.OrderNumber,
                    ClientCode = orderData.ClientCode,
                    DeliveryNote = orderData.DeliveryNoteNumber,
                    InvoiceType = Convert.ToInt32(orderData.ReceiptType),
                    InvoiceLetter = orderData.ReceiptLetter,
                    PointOfSale = orderData.PointOfSale,
                    ReportSaved = false
                };
            }

            return order;
        }

        /// <summary>
        /// Save order as an invoice
        /// </summary>
        /// <param name="dto">RequestCAE DTO</param>
        /// <param name="order">Order data</param>
        /// <param name="client">Client data</param>        
        /// <param name="taxes">Taxes data</param>
        /// <param name="lines">Lines</param>
        /// <param name="fecaeRequest">FECAE Request</param>
        /// <param name="fecaeResponse">FECAE Response</param>
        public string SaveInvoice(RequestCaeDTO dto, OrderHeaderDTO order, OrderClientDataDTO client, List<OrderLineDTO> lines, List<EquipmentDTO> equipments, AfipTaxResultDTO taxes, FECAERequest fecaeRequest, FECAEResponse fecaeResponse)
        {
            if (!ValidResponse(fecaeResponse))
                throw new ApplicationException("It's not possible to save an invoice that was rejected by AFIP");

            var requestDetails = fecaeRequest.FeDetReq.First();
            var responseDetails = fecaeResponse.FeDetResp.First();

            // Invoice Info
            var amountInPesos = Convert.ToDecimal(AfipHelper.FormatNumber(Convert.ToDecimal(requestDetails.ImpTotal) * dto.ExchangeRate));
            var invoiceCode = fecaeRequest.FeCabReq.CbteTipo.ToString().PadLeft(3, '0');
            var invoiceType = Convert.ToInt32(order.ReceiptType);
            var pointOfSale = order.PointOfSale;
            var invoiceLetter = order.ReceiptLetter;
            var invoiceDate = DateTime.ParseExact(requestDetails.CbteFch, "yyyyMMdd", CultureInfo.InvariantCulture);
            var invoiceNumber = GetInvoiceNumber(invoiceLetter, fecaeRequest.FeCabReq.PtoVta.ToString(), responseDetails.CbteHasta.ToString());
            var linesTotal = Convert.ToDecimal(AfipHelper.FormatNumber(lines.Sum(x => x.Amount) - (lines.Sum(x => x.Amount) * (order.OrderDiscount / 100M))));
            var caeDueDate = DateTime.ParseExact(responseDetails.CAEFchVto, "yyyyMMdd", CultureInfo.InvariantCulture);

            // Invoice
            var invoice = new Invoice();
            invoice.InvoiceNumber = invoiceNumber;
            invoice.InvoiceDate = invoiceDate;
            invoice.InvoiceLetter = invoiceLetter;
            invoice.InvoiceCode = invoiceCode;
            invoice.InvoiceType = invoiceType;
            invoice.OrderNumber = order.OrderNumber;
            invoice.OrderDate = order.OrderDate;
            invoice.OrderType = order.OrderType;
            invoice.DeliveryNoteNumber = order.DeliveryNoteNumber;
            invoice.CAE = responseDetails.CAE;
            invoice.CAEDueDate = caeDueDate;
            invoice.ClientCode = order.ClientCode;
            invoice.TaxLiabilityCode = dto.TaxliabilityCode.Trim();
            invoice.BusinessName = client.BusinessName;
            invoice.ClientAddress1 = client.Address1;
            invoice.ClientAddress2 = client.Address2;
            invoice.ClientAddress3 = client.Address3;
            invoice.ZipCode = client.ZipCode;
            invoice.CUIT = client.CUIT;
            invoice.IIBB = client.IB;
            invoice.IVA = client.IVA;
            invoice.ClientDestinationAddress1 = client.DeliveryAddress1;
            invoice.ClientDestinationAddress2 = client.DeliveryAddress2;
            invoice.ClientDestinationAddress3 = client.DeliveryAddress3;
            invoice.ClientDestinationAddress4 = client.DeliveryAddress4;
            invoice.DueDate = GetInvoiceDueDate(order.InvoiceExpirationDays, invoiceDate);
            invoice.PaymentCondition = order.PaymentCondition;
            invoice.QuotationNumber = order.QuotationNumber;
            invoice.Warehouse = order.Warehouse;
            invoice.Observations1 = order.Observations1;
            invoice.Observations2 = order.Observations2;
            invoice.TermsOfDelivery = order.TermsOfDelivery;
            invoice.DeliveryMethod = order.MethodOfDelivery;
            invoice.Salesman = order.Seller;
            invoice.Contact = order.Contact;
            invoice.PurchaseOrderNumber = order.PurchaseOrder;
            invoice.Barcode = GenerateBarCode(invoiceCode, pointOfSale, responseDetails.CAE, caeDueDate);
            invoice.Currency = order.CurrencySymbol;
            invoice.CurrencyCode = order.CurrencyCode;
            invoice.CurrentDayExchangeRate = order.CurrentDayExchangeRate;
            invoice.ExchangeRate = dto.ExchangeRate;
            invoice.Status = (int)OrderStatus.Pending;
            invoice.ContractNumber = order.ContractNumber;
            invoice.ContractFromDate = order.ContractFromDate;
            invoice.ContractToDate = order.ContractToDate;
            invoice.UserEmail = dto.UserEmail;
            invoice.AmountInPesos = amountInPesos;
            invoice.NetAmountTotal = Convert.ToDecimal(requestDetails.ImpNeto);
            invoice.NotTaxedTotal = Convert.ToDecimal(requestDetails.ImpTotConc);
            invoice.ExemptTotal = Convert.ToDecimal(requestDetails.ImpOpEx);
            invoice.LinesTotal = linesTotal;
            invoice.SubTotal = Convert.ToDecimal(requestDetails.ImpNeto);
            invoice.GrandTotal = Convert.ToDecimal(requestDetails.ImpTotal);
            invoice.TextInfo = order.TextInfo;
            invoice.Legend = GetInvoiceLegendHtml(invoiceLetter, order.CurrencySymbol, dto.ExchangeRate, amountInPesos);
            invoice.OrderDiscount = order.OrderDiscount / 100M;
            invoice.PLC = order.PLC;
            invoice.PGC = order.PGC;
            invoice.CC = order.CC;
            
            // Taxes
            if (requestDetails.Iva != null)
            {
                foreach (var tax in requestDetails.Iva)
                {
                    invoice.InvoiceTaxes.Add(
                        new InvoiceTax()
                        {
                            Description = "IVA",
                            Currency = order.CurrencySymbol,
                            Aliquot = AtlasDataContext.IVAs.Where(x => x.AFIPID == tax.Id).First().Aliquot,
                            SubTotalInPesos = Convert.ToDecimal(AfipHelper.FormatNumber(Convert.ToDecimal(tax.BaseImp) * dto.ExchangeRate)),
                            TaxAmountInPesos = Convert.ToDecimal(AfipHelper.FormatNumber(Convert.ToDecimal(tax.Importe) * dto.ExchangeRate)),
                            SubTotal = Convert.ToDecimal(tax.BaseImp),
                            TaxAmount = Convert.ToDecimal(tax.Importe)
                        });
                }
            }

            // Tributes
            if (requestDetails.Tributos != null)
            {
                foreach (var tribute in requestDetails.Tributos)
                {
                    invoice.InvoiceTaxes.Add(
                        new InvoiceTax()
                        {
                            Description = tribute.Desc,
                            Currency = order.CurrencySymbol,
                            Aliquot = Convert.ToDecimal(tribute.Alic) / 100M,
                            SubTotalInPesos = Convert.ToDecimal(AfipHelper.FormatNumber(Convert.ToDecimal(tribute.BaseImp) * dto.ExchangeRate)),
                            TaxAmountInPesos = Convert.ToDecimal(AfipHelper.FormatNumber(Convert.ToDecimal(tribute.Importe) * dto.ExchangeRate)),
                            SubTotal = Convert.ToDecimal(tribute.BaseImp),
                            TaxAmount = Convert.ToDecimal(tribute.Importe)
                        });
                }
            }

            // Lines
            foreach (var line in lines)
            {
                invoice.InvoiceLines.Add(
                    new InvoiceLine()
                    {
                        Currency = order.CurrencySymbol,
                        Code = line.StockCode,
                        Detail1 = line.Description1,
                        Detail2 = line.Description2,
                        Quantity = Math.Abs(line.Quantity),
                        UnitPrice = line.UnitPrice,
                        LineDiscount = line.TotalDiscount / 100M,
                        Amount = line.Amount,
                        TextInfo = line.TextInfo,
                        DeliveryNoteNumber = line.DeliveryNoteNumber
                    });
            }

            // Equipments (only for contract services)
            foreach (var eq in equipments)
            {
                invoice.InvoiceEquipments.Add(
                    new InvoiceEquipment()
                    {
                        LineNumber = eq.LineNumber,
                        ProductCode = eq.StockCode,
                        SerialNumber = eq.SerialNumber,
                        Description = eq.Description
                    });
            }

            AdjustRoundDifferenceInPesos(invoice);

            AtlasDataContext.Invoices.Add(invoice);
            AtlasDataContext.SaveChanges();

            return invoiceNumber;
        }

        /// <summary>
        /// Save order as an invoice
        /// </summary>
        /// <param name="dto">Request CAE DTO</param>
        /// <param name="order">Order data</param>
        /// <param name="client">Client data</param>        
        /// <param name="lines">Lines</param>
        /// <param name="taxes">Taxes data</param>        
        /// <param name="clsFEXRequest">Afip Request</param>
        /// <param name="responseAuthorize">Afip Response</param>
        /// <returns></returns>
        private string SaveInvoice(RequestCaeDTO dto, OrderHeaderDTO order, OrderClientDataDTO client, List<OrderLineDTO> lines, List<EquipmentDTO> equipments, AfipTaxResultDTO taxes, ClsFEXRequest clsFEXRequest, FEXResponseAuthorize responseAuthorize)
        {
            if (!ValidResponse(responseAuthorize))
                throw new ApplicationException("It's not possible to save an invoice that was rejected by AFIP");

            // Invoice Info
            var amountInPesos = Convert.ToDecimal(AfipHelper.FormatNumber(Convert.ToDecimal(clsFEXRequest.Imp_total) * dto.ExchangeRate));
            var invoiceCode = clsFEXRequest.Tipo_cbte.ToString().PadLeft(3, '0');
            var invoiceType = Int32.Parse(order.ReceiptType);
            var pointOfSale = order.PointOfSale;
            var invoiceLetter = order.ReceiptLetter;
            var invoiceDate = DateTime.ParseExact(clsFEXRequest.Fecha_cbte, "yyyyMMdd", CultureInfo.InvariantCulture);
            var invoiceNumber = GetInvoiceNumber(invoiceLetter, clsFEXRequest.Punto_vta.ToString(), clsFEXRequest.Cbte_nro.ToString());
            var linesTotal = Convert.ToDecimal(AfipHelper.FormatNumber(lines.Sum(x => x.Amount) - (lines.Sum(x => x.Amount) * (order.OrderDiscount / 100M))));
            var caeDueDate = DateTime.ParseExact(responseAuthorize.FEXResultAuth.Fch_venc_Cae, "yyyyMMdd", CultureInfo.InvariantCulture);

            // Invoice
            var invoice = new Invoice();
            invoice.InvoiceNumber = invoiceNumber;
            invoice.InvoiceDate = invoiceDate;
            invoice.InvoiceLetter = invoiceLetter;
            invoice.InvoiceCode = invoiceCode;
            invoice.InvoiceType = invoiceType;
            invoice.OrderNumber = order.OrderNumber;
            invoice.OrderDate = order.OrderDate;
            invoice.OrderType = order.OrderType;
            invoice.DeliveryNoteNumber = order.DeliveryNoteNumber;
            invoice.CAE = responseAuthorize.FEXResultAuth.Cae;
            invoice.CAEDueDate = caeDueDate;
            invoice.PaymentCondition = order.PaymentCondition;
            invoice.ClientCode = order.ClientCode;
            invoice.TaxLiabilityCode = dto.TaxliabilityCode.Trim();
            invoice.BusinessName = client.BusinessName;
            invoice.ClientAddress1 = client.Address1;
            invoice.ClientAddress2 = client.Address2;
            invoice.ClientAddress3 = client.Address3;
            invoice.ZipCode = client.ZipCode;
            invoice.CUIT = client.CUIT;
            invoice.IIBB = client.IB;
            invoice.IVA = client.IVA;
            invoice.ClientDestinationAddress1 = client.DeliveryAddress1;
            invoice.ClientDestinationAddress2 = client.DeliveryAddress2;
            invoice.ClientDestinationAddress3 = client.DeliveryAddress3;
            invoice.ClientDestinationAddress4 = client.DeliveryAddress4;
            invoice.DueDate = GetInvoiceDueDate(order.InvoiceExpirationDays, invoiceDate);            
            invoice.QuotationNumber = order.QuotationNumber;
            invoice.Warehouse = order.Warehouse;
            invoice.Observations1 = order.Observations1;
            invoice.Observations2 = order.Observations2;
            invoice.TermsOfDelivery = order.TermsOfDelivery;
            invoice.DeliveryMethod = order.MethodOfDelivery;
            invoice.Salesman = order.Seller;
            invoice.Contact = order.Contact;
            invoice.PurchaseOrderNumber = order.PurchaseOrder;
            invoice.Barcode = GenerateBarCode(invoiceCode, pointOfSale, responseAuthorize.FEXResultAuth.Cae, caeDueDate);
            invoice.Currency = order.CurrencySymbol;
            invoice.CurrencyCode = order.CurrencyCode;
            invoice.CurrentDayExchangeRate = order.CurrentDayExchangeRate;
            invoice.ExchangeRate = dto.ExchangeRate;
            invoice.Status = (int)OrderStatus.Pending;
            invoice.ContractNumber = order.ContractNumber;
            invoice.ContractFromDate = order.ContractFromDate;
            invoice.ContractToDate = order.ContractToDate;
            invoice.UserEmail = dto.UserEmail;
            invoice.AmountInPesos = amountInPesos;
            invoice.NetAmountTotal = Convert.ToDecimal(AfipHelper.FormatNumber(taxes.NetAmountTotal));
            invoice.NotTaxedTotal = Convert.ToDecimal(AfipHelper.FormatNumber(taxes.NotTaxedTotal));
            invoice.ExemptTotal = Convert.ToDecimal(AfipHelper.FormatNumber(taxes.ExemptTotal));
            invoice.LinesTotal = linesTotal;
            invoice.SubTotal = Convert.ToDecimal(clsFEXRequest.Imp_total);
            invoice.GrandTotal = Convert.ToDecimal(clsFEXRequest.Imp_total);
            invoice.TextInfo = order.TextInfo;
            invoice.Legend = GetInvoiceLegendHtml(invoiceLetter, order.CurrencySymbol, dto.ExchangeRate, amountInPesos);
            invoice.OrderDiscount = order.OrderDiscount / 100M;
            invoice.ExportPermit = (int)dto.ExportPermitId;
            invoice.ExportType = (int)dto.ExportTypeId;
            invoice.PLC = order.PLC;
            invoice.PGC = order.PGC;
            invoice.CC = order.CC;

            // Taxes
            foreach (var tax in taxes.IVAs)
            {
                invoice.InvoiceTaxes.Add(
                    new InvoiceTax()
                    {
                        Description = "IVA",
                        Currency = order.CurrencySymbol,
                        Aliquot = tax.Aliquot,
                        SubTotal = linesTotal,
                        TaxAmount = Convert.ToDecimal(AfipHelper.FormatNumber(tax.TotalTaxAmount))
                    });
            }

            foreach (var tribute in taxes.Tributes)
            {
                invoice.InvoiceTaxes.Add(
                    new InvoiceTax()
                    {
                        Description = tribute.Description,
                        Currency = order.CurrencySymbol,
                        Aliquot = tribute.Aliquot,
                        SubTotal = linesTotal,
                        TaxAmount = Convert.ToDecimal(AfipHelper.FormatNumber(tribute.Amount))
                    });
            }

            // Lines
            foreach (var line in lines)
            {
                invoice.InvoiceLines.Add(
                    new InvoiceLine()
                    {
                        Currency = order.CurrencySymbol,
                        Code = line.StockCode,
                        Detail1 = line.Description1,
                        Detail2 = line.Description2,
                        Quantity = Math.Abs(line.Quantity),
                        UnitPrice = line.UnitPrice,
                        LineDiscount = line.TotalDiscount / 100M,
                        Amount = line.Amount,
                        TextInfo = line.TextInfo,
                        DeliveryNoteNumber = line.DeliveryNoteNumber
                    });
            }

            // Equipments (only for contract services)
            foreach (var eq in equipments)
            {
                invoice.InvoiceEquipments.Add(
                    new InvoiceEquipment()
                    {
                        LineNumber = eq.LineNumber,
                        ProductCode = eq.StockCode,
                        SerialNumber = eq.SerialNumber,
                        Description = eq.Description
                    });
            }

            AdjustRoundDifferenceInPesos(invoice);

            AtlasDataContext.Invoices.Add(invoice);
            AtlasDataContext.SaveChanges();

            return invoiceNumber;
        }

        /// <summary>
        /// Adjust taxes difference in Argentinian pesos
        /// </summary>
        /// <param name="invoice">Invoice</param>
        public void AdjustRoundDifferenceInPesos(Invoice invoice)
        {
            var invoiceAmountInPesos = invoice.AmountInPesos;
            var calculatedAmountInPesos = CalculateAmountInPesos(invoice);
            if (invoiceAmountInPesos != calculatedAmountInPesos)
            {
                var difference = invoiceAmountInPesos - calculatedAmountInPesos;
                var firstIVA = invoice.InvoiceTaxes.Where(x => x.Description == "IVA").FirstOrDefault();
                if (firstIVA != null)
                {
                    firstIVA.TaxAmountInPesos = firstIVA.TaxAmountInPesos + difference;
                    SendAdminEmail(invoice.OrderNumber, string.Format( ServicesResources.EMAIL_ADMIN_PESOS_ADJUST, invoice.AmountInPesos, calculatedAmountInPesos));
                }
                else
                {
                    SendAdminEmail(invoice.OrderNumber, ServicesResources.EMAIL_ADMIN_PESOS_ADJUST_NO_IVA);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="amount"></param>
        /// <param name="exchangeRate"></param>
        /// <returns></returns>
        public decimal CalculateAmountInPesos(decimal amount, decimal exchangeRate)
        {
            return Convert.ToDecimal(AfipHelper.FormatNumber(amount * exchangeRate));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="amount"></param>
        /// <param name="exchangeRate"></param>
        /// <returns></returns>
        public decimal CalculateAmountInPesos(double amount, decimal exchangeRate)
        {
            return CalculateAmountInPesos(Convert.ToDecimal(amount), exchangeRate);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="invoice"></param>
        /// <returns></returns>
        public decimal CalculateAmountInPesos(Invoice invoice)
        {
            return invoice.InvoiceTaxes.Sum(x => x.TaxAmountInPesos ?? 0M)
                + invoice.InvoiceTaxes.Where(x => x.Description == "IVA").Sum(x => x.SubTotalInPesos ?? 0M)
                + CalculateAmountInPesos(invoice.ExemptTotal ?? 0M, invoice.ExchangeRate)
                + CalculateAmountInPesos(invoice.NotTaxedTotal ?? 0M, invoice.ExchangeRate);
        }

        /// <summary>
        /// Get invoice due date
        /// </summary>
        /// <param name="expirationDays">Expiration days</param>
        /// <param name="invoiceDate">Invoice date</param>
        /// <returns>The incoice Due Date</returns>
        private DateTime GetInvoiceDueDate(int expirationDays, DateTime invoiceDate)
        {
            return invoiceDate.AddDays(expirationDays);
        }

        /// <summary>
        /// Get invoice number
        /// </summary>
        /// <param name="invoiceLetter">Invoice letter</param>
        /// <param name="pointOfSale">Point of sale</param>
        /// <param name="receiptNumber">Receipt number</param>
        /// <returns>A string with the invoice number</returns>
        private string GetInvoiceNumber(string invoiceLetter, string pointOfSale, string receiptNumber)
        {
            return string.Format("{0}{1}-{2}", invoiceLetter, pointOfSale.PadLeft(5, '0'), receiptNumber.PadLeft(8, '0'));
        }

        /// <summary>
        /// Generate barcode 
        /// </summary>        
        /// <param name="invoiceCode">Invoice code</param>
        /// <param name="pointOfSale">Point of sale</param>
        /// <param name="cae">CAE number</param>        
        /// <param name="invoiceDate">Invoice date</param>
        /// <returns></returns>
        public string GenerateBarCode(string invoiceCode, string pointOfSale, string cae, DateTime caeDueDate)
        {
            var companyCUIT = ((NameValueCollection)ConfigurationManager.GetSection("afipAuth"))["cuit"];
            var barcode = string.Concat(companyCUIT, invoiceCode.Trim(), pointOfSale, cae, caeDueDate.ToString("yyyyMMdd"));
            var verificationDigit = GetVerificationDigit(barcode);
            return string.Concat(barcode, verificationDigit);
        }

        /// <summary>
        /// Calculates verification number
        /// </summary>
        /// <param name="barcode">Barcode</param>
        /// <returns>The verification number to use with the barcode</returns>
        public int GetVerificationDigit(string barcode)
        {
            var index = 0;
            var oddNumbers = 0;
            var evenNumbers = 0;

            // Iterate all characters
            foreach (var c in barcode.ToCharArray())
            {
                if (IsOdd(index))
                {
                    oddNumbers += int.Parse(c.ToString());
                }
                else
                {
                    evenNumbers += int.Parse(c.ToString());
                }
                index++;
            }

            // Get subtotal
            var subTotal = (evenNumbers * 3) + oddNumbers;

            // Get the nearest multiple of 10
            var multipleNumber = 0;
            var foundMultiple = false;

            while (!foundMultiple)
            {
                multipleNumber += 10;
                if (multipleNumber >= subTotal)
                    foundMultiple = true;
            }

            // Return verification digit
            return multipleNumber - subTotal;
        }

        /// <summary>
        /// Determines if a number is an odd number
        /// </summary>
        /// <param name="value">Number</param>
        /// <returns>True if the number is an odd number. False if it is an even number.</returns>
        private bool IsOdd(int value)
        {
            return value % 2 != 0;
        }

        /// <summary>
        /// Get the invoice legend
        /// </summary>
        /// <param name="invoiceLetter">Invoice letter</param>
        /// <param name="currencySymbol">Invoice symbol</param>
        /// <param name="exchangeRate">Exchange rate</param>
        /// <param name="amountInPesos">Amount in pesos</param>
        /// <returns>An HTML with the invoice legend</returns>
        public string GetInvoiceLegendHtml(string invoiceLetter, string currencySymbol, decimal exchangeRate, decimal amountInPesos)
        {
            var sentence1 = "";
            var sentence2 = "";
            var sentence3 = "";
            var sentence4 = "";
            var sentence5 = "";
            var sentence6 = "";
            var sentence7 = "";
            var formatedAmountInPesos = amountInPesos.ToString("C");

            // Sentences
            sentence1 = ServicesResources.ATLAS_INVOICE_SENTENCE1;
            sentence2 = ServicesResources.ATLAS_INVOICE_SENTENCE2;
            sentence3 = ServicesResources.ATLAS_INVOICE_SENTENCE3;
            sentence4 = ServicesResources.ATLAS_INVOICE_SENTENCE4;
            sentence5 = string.Format(ServicesResources.ATLAS_INVOICE_SENTENCE5, exchangeRate);
            sentence6 = ServicesResources.ATLAS_INVOICE_SENTENCE6;
            sentence7 = ServicesResources.ATLAS_INVOICE_SENTENCE7;
            
            var legend = "<div>";
            var invoiceInPesos = currencySymbol == "$";

            // Build invoice's legend
            if (invoiceLetter.ToUpper() == "A")
            {
                if (invoiceInPesos)
                {
                    legend += string.Format("<p>{0}</p>", sentence1);
                    legend += string.Format("<p>{0}</p>", sentence2);
                    legend += string.Format("<p>{0}</p>", sentence3);
                }
                else
                {
                    legend += string.Format("<p>{0}</p>", sentence5);
                    legend += string.Format("<p>{0}</p>", ServicesResources.INVOICE_TOTAL + formatedAmountInPesos);
                    legend += string.Format("<p>{0}</p>", sentence1);
                }
            }
            else if (invoiceLetter.ToUpper() == "B")
            {
                if (invoiceInPesos)
                {
                    legend += string.Format("<p>{0}</p>", sentence5);
                    legend += string.Format("<p>{0}</p>", sentence2);
                    legend += string.Format("<p>{0}</p>", sentence4);
                    legend += string.Format("<p>{0}</p>", sentence3);
                }
                else
                {
                    legend += string.Format("<p>{0}</p>", sentence5);
                    legend += string.Format("<p>{0}</p>", ServicesResources.INVOICE_TOTAL + formatedAmountInPesos);
                    legend += string.Format("<p>{0}</p>", sentence1);
                    legend += string.Format("<p>{0}</p>", sentence4);
                }
            }
            else
            {
                // InvoiceLetter == "E"
                if(invoiceInPesos)
                {
                    legend += string.Format("<p>{0}</p>", sentence1);
                    legend += string.Format("<p>{0}</p>", sentence6);
                    legend += string.Format("<p>{0}</p>", sentence7);
                }
                else
                {
                    legend += string.Format("<p>{0}</p>", sentence5);
                    legend += string.Format("<p>{0}</p>", ServicesResources.INVOICE_TOTAL + formatedAmountInPesos);
                    legend += string.Format("<p>{0}</p>", sentence1);
                    legend += string.Format("<p>{0}</p>", sentence6);
                    legend += string.Format("<p>{0}</p>", sentence7);
                }
            }

            legend += "</div>";
            return legend;
        }

        /// <summary>
        /// Determines if we got a valid response with a CAE number
        /// </summary>
        /// <param name="fecaeResponse">Afip Response</param>
        /// <returns>True if it was approved. False otherwise.</returns>
        private bool ValidResponse(FECAEResponse fecaeResponse)
        {
            return (fecaeResponse != null && fecaeResponse.FeCabResp != null && fecaeResponse.FeCabResp.Resultado == "A");
        }

        /// <summary>
        /// Determines if we got a valid response with a CAE number for export invoices
        /// </summary>
        /// <param name="responseAuthorize">Afip Response</param>
        /// <returns>True if it was approved. False otherwise.</returns>
        private bool ValidResponse(FEXResponseAuthorize responseAuthorize)
        {
            return (responseAuthorize != null && responseAuthorize.FEXResultAuth != null && responseAuthorize.FEXResultAuth.Resultado == "A");
        }

        /// <summary>
        /// Get report URL
        /// </summary>
        /// <param name="receiptType">Receipt type</param>
        /// <param name="invoiceNumber">Invoice Number</param>
        /// <returns>Report URL</returns>
        private string GetReportUrl(string receiptType, string invoiceNumber)
        {
            var reportUrl = "";
            switch (receiptType)
            {
                case "0":
                    reportUrl += "/invoices/";
                    break;
                case "1":
                    reportUrl += "/debit-notes/";
                    break;
                case "2":
                    reportUrl += "/credit-notes/";
                    break;
            }

            return string.Concat(reportUrl, invoiceNumber, ".pdf");
        }

        /// <summary>
        /// Get addtional text info for a particular order line
        /// </summary>
        /// <param name="orderNumber">Order number</param>
        /// <param name="orderType">Order type</param>
        /// <param name="lineNumber">Line number</param>
        /// <returns>Get text line info</returns>
        public string GetLineInfo(string orderNumber, string orderType, string lineNumber)
        {
            var textInfo = "";

            var lstTextData = ScalaDataContext.SP_ComprobantesTextos(orderNumber, orderType, lineNumber);
            foreach (var textData in lstTextData)
                textInfo +=  Environment.NewLine + textData.TextInfo;

            return textInfo;
        }

        #region Afip Helpers

        /// <summary>
        /// Get Measurement Unit
        /// </summary>
        /// <param name="scalaMeasurementUnitId">Scala Measurement Unit Id</param>
        /// <returns>Afip Measurement Unit ID</returns>
        private int GetAfipMeasurementUnit(int scalaMeasurementUnitId)
        {
            return AtlasDataContext.MeasurementUnits.Where(x => x.ScalaId == scalaMeasurementUnitId).First().AfipId.Value;
        }

        /// <summary>
        /// Get Afip Country ID
        /// </summary>
        /// <param name="scalaContryCode">Scala Contry Code</param>
        /// <returns>Afip Country ID</returns>
        private short GetAfipCountryId(string scalaContryCode)
        {
            return Convert.ToInt16(AtlasDataContext.Countries.Where(x => x.ScalaId == scalaContryCode).First().AfipId.Value);
        }

        /// <summary>
        /// Get Afip Permission Type
        /// </summary>
        /// <param name="receiptType">Receipt type</param>
        /// <param name="exportType">Export Type</param>
        /// <returns>Afip Permission Type</returns>
        private string GetAfipPermission(short receiptType, short exportType, short exportPermit)
        {
            if ((receiptType == (int)AfipReceiptTypeE.DebitNote || receiptType == (int)AfipReceiptTypeE.CreditNote)
            || (receiptType == (int)AfipReceiptTypeE.Invoice && (exportType == (int)AfipExportType.Services || exportType == (int)AfipExportType.Other)))
                return "";

            if (receiptType == (int)AfipReceiptTypeE.Invoice && exportPermit == (int)AfipExportPermit.Yes)
                return "S";

            return "N";
        }

        /// <summary>
        /// Get AFIP currency id by Scala currency code
        /// </summary>
        /// <param name="currencyCode">Scala currency code</param>
        /// <returns>Afip Currency Id</returns>
        private string GetAfipCurrencyId(int currencyCode)
        {
            var currency = AtlasDataContext.Currencies.Where(x => x.SCALAID == currencyCode).FirstOrDefault();
            return (currency != null) ? currency.AFIPID : "PES";
        }

        /// <summary>
        /// Get AFIP Tribute id by Scala tribute description
        /// </summary>
        /// <param name="description">Description</param>
        /// <returns>Afip tribute Id</returns>
        private short GetAfipTributeId(string description)
        {
            var tribute = AtlasDataContext.Tributes.Where(x => x.SCALAID == description).FirstOrDefault();
            return (tribute != null) ? Convert.ToInt16(tribute.AFIPID) : Convert.ToInt16(99);
        }

        /// <summary>
        /// Get AFIP IVA id by Scala description and aliquot
        /// </summary>
        /// <param name="description">IVA Description</param>
        /// <param name="aliquot">Aliquot</param>
        /// <returns>Afip IVA Id</returns>
        private int GetAfipIVAId(string description, decimal aliquot)
        {
            return AtlasDataContext.IVAs.Where(x => x.SCALAID == description && x.Aliquot == aliquot).First().AFIPID;
        }        

        /// <summary>
        /// Get AFIP recept type by Scala receipt code
        /// </summary>
        /// <param name="receiptLetter">Receipt letter</param>
        /// <param name="receiptType">Receipt type</param>
        /// <returns>The matching receipt type</returns>
        private int GetAfipReceiptType(string receiptLetter, string receiptType)
        {
            var rt = AtlasDataContext.ReceiptTypes.Where(x => x.ScalaInvoiceLetter == receiptLetter && x.ScalaInvoiceType == receiptType).First();
            return Convert.ToInt16(rt.AfipID);
        }

        /// <summary>
        /// Get Afip concept ID
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>
        /// 1 for Products
        /// 2 for Services
        /// 3 for Products and Services
        /// </returns>
        private int GetConcept(OrderHeaderDTO order)
        {
            // Products
            if (order.OrderType == "OV" && (order.SalesOrderType == 1 || order.SalesOrderType == 2 || order.SalesOrderType == 4))
                return 1;

            // Services
            if ((order.OrderType == "CS") || (order.OrderType == "OS" && order.ServiceMaterial == "SE"))
                return 2;

            // Products ans Services
            if (order.OrderType == "OS" && order.ServiceMaterial == "MS")
                return 3;

            return 0;
        }

        /// <summary>
        /// Get an approved invoice from Afip
        /// </summary>
        /// <param name="receiptType">Receipt type</param>
        /// <param name="receiptNumber">Receipt number</param>
        /// <param name="pointOfSale">Point of sale</param>
        /// <returns>Invoice data from Afip Servers</returns>
        public FECompConsultaResponse GetInvoiceFromAfip(int receiptType, long receiptNumber, int pointOfSale)
        {
            var afipConnector = new WSFEConnector();
            return afipConnector.ObtenerComprobante(receiptType, receiptNumber, pointOfSale);
        }

        #endregion
    }
}
