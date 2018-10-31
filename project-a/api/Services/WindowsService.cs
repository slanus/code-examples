using Atlas.FactElec.Core.DTO;
using Atlas.FactElec.Core.Enum;
using Atlas.FactElec.Data.ADO;
using Atlas.FactElec.Data.EntityFramework;
using Atlas.FactElec.Utils;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity.Validation;

namespace Atlas.FactElec.Services
{
    public static class WindowsService
    {
        /// <summary>
        /// Close orders in our database if it's already closed in Scala
        /// </summary>
        public static void CloseOrders()
        {
            try
            {
                var ordersService = new OrdersWithCaeService(new AtlasDataContext(), new ScalaDataContext());
                var pendingOrders = GetPendingOrders(ordersService);

                foreach (var pendingOrder in pendingOrders)
                {
                    var invoiceNumber = GetScalaInvoiceNumber(pendingOrder);
                    if (ordersService.IsClosed(invoiceNumber, pendingOrder.OrderNumber, pendingOrder.OrderType))
                    { 
                        var transactionNumber = ordersService.GetTransactionNumber(invoiceNumber);
                        ordersService.CloseOrder(pendingOrder, transactionNumber);
                    }
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
                LogHelper.Log.Fatal(ex);
            }
        }

        /// <summary>
        /// Send an email to every user that still needs to close an order in Scala
        /// </summary>
        public static void SendEmails()
        {
            var ordersService = new OrdersWithCaeService(new AtlasDataContext(), new ScalaDataContext());
            var pendingOrders = GetPendingOrders(ordersService);
            var templatePath = MailHelper.GetTemplatePath(EmailType.ClosePendingOrder);

            foreach (var pendingOrder in pendingOrders)
            {
                var invoiceNumber = GetScalaInvoiceNumber(pendingOrder);
                if (!ordersService.IsClosed(invoiceNumber, pendingOrder.OrderNumber, pendingOrder.OrderType))
                {
                    var emailData = new EmailDataDTO
                    {
                        OrderNumber = pendingOrder.OrderNumber,
                        OrderDate = pendingOrder.OrderDate,
                        ClientCode = pendingOrder.ClientCode,
                        BussinessName = pendingOrder.BusinessName,
                        PurchaseOrder = pendingOrder.PurchaseOrderNumber,
                        ReceiptNumber = pendingOrder.InvoiceNumber,
                        ReceiptType = pendingOrder.InvoiceType.ToString(),
                        EmailType = EmailType.ClosePendingOrder,
                        DeliveryNotes = string.Join(" ", pendingOrder.InvoiceLines
                                            .Select(x => x.DeliveryNoteNumber).Distinct().OrderBy(x => x)
                                            .ToList())
                    };

                    if (!string.IsNullOrEmpty(pendingOrder.UserEmail))
                        MailHelper.Send(pendingOrder.UserEmail, ServicesResources.EMAIL_PENDING_SUBJECT, templatePath, emailData);
                }
            }
        }

        /// <summary>
        /// Get pending orders
        /// </summary>
        /// <param name="ordersService">Order service</param>
        /// <returns>A list of pending orders</returns>
        private static List<Invoice> GetPendingOrders(OrdersWithCaeService ordersService)
        {
            var userService = new UsersService(new AtlasDataContext(), new ScalaDataContext());
            return ordersService.ListPending();
        }

        /// <summary>
        /// Get Scala Invoice Number
        /// </summary>
        /// <param name="pendingOrder">Pending Order</param>
        /// <returns>The invoice numer in the same format it is stored in Scala Database</returns>
        public static string GetScalaInvoiceNumber(Invoice pendingOrder)
        {
            var invoiceNumber = "";

            switch ((InvoiceType)pendingOrder.InvoiceType)
            {
                case InvoiceType.Invoice:
                    invoiceNumber = pendingOrder.InvoiceNumber;
                    break;
                case InvoiceType.DebitNote:
                    invoiceNumber = string.Concat(pendingOrder.InvoiceNumber, "D");
                    break;
                case InvoiceType.CreditNote:
                    invoiceNumber = string.Concat(pendingOrder.InvoiceNumber, "C");
                    break;
            }

            return invoiceNumber;
        }
    }
}