/**
 * @this vm
 * @ngdoc controller
 * @name inspinia.modalCaeCtrl:modalCaeCtrl
 *
 * @description
 * request cae modal controller
 */
function modalCaeCtrl(
    $scope,
    $rootScope,
    $http,
    $location,
    $translate,
    toaster,
    $window,
    ngAuthSettings,
    orderNumber,
    files,
    $q
) {

    // Init scope variables    
    $scope.datepicker = new Date();
    $scope.format = "dd/MM/yyyy";
    $scope.pointOfSale = "";
    $scope.receiptLetter = "";
    $scope.receiptType = "";
    $scope.currencySymbol = "";
    $scope.newEmail = "";
    $scope.sendEmailCopy = true;

    $scope.selectedEmail = [];
    $scope.pointOfSales = [];

    $scope.exportationTypes = [{
            id: 1,
            description: $translate.instant(" EXPORTATIONSDEFINITIVE")
        },
        {
            id: 2,
            description: $translate.instant(" EXPORTATIONSSERVICES")
        },
        {
            id: 4,
            description: $translate.instant("OTHEREXPORTATIONS")
        }
    ];

    $scope.exportationPermits = [{
            id: 0,
            description: $translate.instant("EXPORTATIONPERMITN")
        },
        {
            id: 1,
            description: $translate.instant("EXPORTATIONPERMITY")
        }
    ]

    $scope.exchangeRateTypes = [{
            id: 1,
            description: $translate.instant("EXCHANGERATEFIXED")
        },
        {
            id: 2,
            description: $translate.instant("EXCHANGERATEVARIABLE")
        }
    ];

    $scope.receiptTypes = [{
            id: '0',
            description: $translate.instant(" INVOICE")
        },
        {
            id: '1',
            description: $translate.instant(" DEBITNOTE")
        },
        {
            id: '2',
            description: $translate.instant("CREDITNOTE")
        }
    ];

    $scope.receiptLetters = [{
            id: 'A',
            description: 'A'
        },
        {
            id: 'B',
            description: 'B'
        },
        {
            id: 'E',
            description: 'E'
        }
    ];

    /**
     * @ngdoc method
     * @name modalCaeCtrl#init
     *
     * @methodOf
     * inspinia.controller:modalCaeCtrl
     *
     * @description
     * init popup
     *      
     */
    $scope.init = function() {
        $scope.loading = true;

        var promises = [];
        var pointOfSalesPromise = $http.get(ngAuthSettings.apiServiceBaseUri + "api/v1/OrdersWithoutCae/ListPointOfSales", {
            headers: {
                'Cache-Control': 'no-cache'
            }
        });
        var headerDataPromise = $http.get(ngAuthSettings.apiServiceBaseUri + "api/v1/OrdersWithoutCae/GetDetailsHeader", {
            params: {
                orderNumber: orderNumber
            },
            headers: {
                'Cache-Control': 'no-cache'
            }
        });
        var taxliabilityCodesPromise = $http.get(ngAuthSettings.apiServiceBaseUri + "api/v1/Client/ListTaxliabilityCodes", {
            headers: {
                'Cache-Control': 'no-cache'
            }
        });

        promises.push(pointOfSalesPromise);
        promises.push(headerDataPromise);
        promises.push(taxliabilityCodesPromise);

        $q.all(promises)
            .then(function(data) {

                // Point of sale                
                if (data[0].status == 200) {
                    var pointOfSales = data[0].data;
                    for (var i = 0; i < pointOfSales.length; i++) {
                        $scope.pointOfSales.push({
                            id: pointOfSales[i],
                            description: pointOfSales[i]
                        });
                    }
                }

                // Header Data                
                if (data[1].status == 200) {                  
                    var headerData = data[1].data;                    
                    $scope.orderNumber = headerData.OrderNumber;
                    $scope.code = headerData.ClientCode;
                    $scope.client = headerData.BusinessName;
                    $scope.date = headerData.Date;
                    $scope.deliveryNote = headerData.DeliveryNoteNumber;
                    $scope.receiptType = headerData.ReceiptType;
                    $scope.originalReceiptType = headerData.ReceiptType;
                    $scope.receiptLetter = headerData.ReceiptLetter;
                    $scope.type = headerData.OrderType;
                    $scope.isNegative = headerData.Amount < 0;
                    $scope.isLetterE = $scope.receiptLetter === "E" ? true : false;
                    $scope.pointOfSale = (headerData.PointOfSale) ? headerData.PointOfSale.trim() : "";
                    $scope.currencySymbol = data.CurrencyCode === 0 ? "$" : "US$";
                    listDeliveryAddresses($scope.code, $scope.orderNumber, $scope.type);
                    listEmails($scope.code);

                    // Exchange rate
                    var originalExchangeRate = headerData.UseManualExchangeRate ? 2 : 1;
                    $scope.exchangeRate = headerData.OrderExchangeRate;                    
                    $scope.currentDayExchangeRate = headerData.CurrentDayExchangeRate;
                    $scope.manualExchangeRate = headerData.ManualExchangeRate;
                    $scope.disableExchangeRate = (headerData.ReceiptType === "2" || headerData.CurrencyCode === 0);
                    $scope.exchangeRateTypeId = originalExchangeRate;
                    $scope.originalExchangeRateId = originalExchangeRate;
                    $scope.taxliability = (headerData.TaxliabilityCode === null) ? "" : headerData.TaxliabilityCode.trim();

                    if (headerData.OrderType.trim() === "CS") {
                        $scope.nextInvDate = data.nextInvDate;
                    }

                    $scope.loading = false;
                    $scope.submitting = false;
                }

                // TaxLiability Codes                
                if (data[2].status == 200) {
                    var taxLiabilityCodes = data[2].data;
                    $scope.taxabilities = taxLiabilityCodes;
                }
            })
            .catch(function() {
              toaster.pop({
                type: "warning",
                title: $translate.instant("MODALERROR"),
                body: $translate.instant("MODALEXCEPTION"),                
                showCloseButton: true,
                progressBar: true
            });
        });
    }

    /**
     * @ngdoc method
     * @name modalCaeCtrl#changeExchangeRate
     *
     * @methodOf
     * inspinia.modalCaeCtrl:modalCaeCtrl
     *
     * @description
     * Enanble or disable exchange rate textbox
     *      
     */
    $scope.changeExchangeRate = function() {
        $scope.exchangeRate = ($scope.exchangeRateTypeId === 1) ? $scope.currentDayExchangeRate : $scope.manualExchangeRate;        
    };


    /**
      * @ngdoc method
      * @name modalCaeCtrl#_FUNCTION_NAME_
      *
      * @methodOf
      * inspinia.controller:modalCaeCtrl
      *
      * @description
      * triggered whenever the receipt type changes. 
      * enables or disables the exchangeRate input.
      *      
    */
    $scope.changeReceiptType = function() {
      $scope.disableExchangeRate = ($scope.receiptType === "2" || $scope.currencyCode === 0);
      if($scope.receiptType === "2") {
        $scope.exchangeRateTypeId = 2;
        $scope.exchangeRate = 0;
      } else {
        $scope.exchangeRateTypeId = 1;
        $scope.exchangeRate = $scope.currentDayExchangeRate;
      }
    }

    /**
     * @ngdoc method
     * @name modalCaeCtrl#addNew
     *
     * @methodOf
     * inspinia.controller:modalCaeCtrl
     *
     * @description
     * add a new email
     *
     * @param {bool} value new email added      
     */
    $scope.addNew = function(value) {
        $scope.addNewEmail = value;
    };

    /**
     * @ngdoc method
     * @name cancel
     *
     * @methodOf
     * inspinia.modalCaeCtrl:modalCaeCtrl
     *
     * @description
     * close modal instance popup
     *       
     */
    $scope.cancel = function() {
        $rootScope.modalInstance.close();
    };

    /**
     * @ngdoc method
     * @name modalCaeCtrl#dataValidation
     *
     * @methodOf
     * inspinia.modalCaeCtrl:modalCaeCtrl
     *
     * @description
     * validate form data
     *      
     * @return {bool} bool true if valid. False otherwise
     */
    $scope.dataValidation = function() {
        var isValid = true;
        var body = "<ul style='padding-left:0;'>";

        $scope.exRat = isNumericExchangeRate($scope.exchangeRate);

        if (!$scope.invoiceForm.$valid) {
            if ($scope.pointOfSale.trim() === "") {
                body = $translate.instant("MESSAGEBODYPOINTSALE");
            } else if ($scope.receiptLetter.trim() === "") {
                body = $translate.instant("MESSAGEBODYRECEIPTLETTER");
            } else {
                body = $translate.instant("MESSAGEBODYFORM");
            }

            isValid = false;
        }

        if (!$scope.exRat) {
            body += "<li>" + $translate.instant("MESSAGEBODYEXRATE") + "</li>";
            isValid = false;
        }

        if (!angular.isDate($scope.datepicker)) {
            body += "<li>" + $translate.instant("MESSAGEBODYDATEPICKER") + "</li>";
            isValid = false;
        }

        if ($scope.exchangeRate === 0) {
            body += "<li>" + $translate.instant("MESSAGEEXCHANGERATEZERO") + "</li>";
            isValid = false;
        }

        if (!$scope.receiptType) {
            body += "<li>" + $translate.instant("MESSAGEINVALIDRECEIPTTYPE") + "</li>";
            isValid = false;
        }

        if (!$scope.pointOfSale || $scope.pointOfSale === '0') {
            body += "<li>" + $translate.instant("MESSAGEINVALIDPOINTOFSALE") + "</li>";
            isValid = false;
        }

        if (!$scope.receiptLetter) {
            body += "<li>" + $translate.instant("MESSAGEINVALIDLETTER") + "</li>";
            isValid = false;
        }

        if ($scope.isLetterE) {
            if (isEmpty($scope.exportTypeId)) {
                body += "<li>" + $translate.instant("MESSAGEEXPORTTYPENOTDETECTED") + "</li>";
                isValid = false;
            }
        }

        body += "</ul>"

        if (!isValid) {
            toaster.pop({
                type: "warning",
                title: $translate.instant("VALIDATIONTITLE"),
                body: body,
                bodyOutputType: 'trustedHtml',
                showCloseButton: true,
                progressBar: true
            });
        }

        return isValid;
    }

    /**
      * @ngdoc method
      * @name modalCaeCtrl#send
      *
      * @methodOf
      * inspinia.modalCaeCtrl:modalCaeCtrl
      *
      * @description
      * send cae request to AFIP
      *      
    */
    $scope.send = function() {
      // Validate form    
      $scope.formatEmail = /^[a-z]+[a-z0-9._]+@[a-z0-9]([a-z0-9-]*[a-z0-9])?(\.[a-z0-9]([a-z0-9-]*[a-z0-9])?)*$/i;

      if (!$scope.dataValidation()) {
          return false;
      }
      
      // Submit valid data
      $scope.submitting = true;

      if ($scope.addNewEmail && $scope.newEmail) {
          $scope.selectedEmail.push($scope.newEmail);
      }
      
      var data = {
          ReceiptLetter: $scope.receiptLetter,
          ReceiptType: $scope.receiptType,
          PointOfSale: $scope.pointOfSale,
          ClientCode: $scope.code,
          OrderType: $scope.type,
          OrderNumber: $scope.orderNumber,
          DeliveryNote: $scope.deliveryNote,
          Date: $scope.datepicker.toDateString(),
          TaxliabilityCode: $scope.taxliability,
          ExchangeRate: $scope.exchangeRate,
          UpdateExchangeRate: $scope.exchangeRateTypeId === 2,
          ExchangeRateId: $scope.exchangeRateTypeId,
          DeliveryAddress: $scope.deliveryAddress,
          DeliveryAddressId: $scope.deliveryAddressId,
          SendEmailCopy: $scope.sendEmailCopy,
          ExportTypeId: $scope.exportTypeId,
          ExportPermitId: 0,
          Emails: $scope.selectedEmail,
          FileAttachments: files.files
      };

      var type = "";
      var title = "";
      var message = "";
      $scope.result = "";

      $http
          .post(
              ngAuthSettings.apiServiceBaseUri + "api/v1/OrdersWithoutCae/RequestCAE",
              data, {
                  headers: {
                      'Cache-Control': 'no-cache'
                  }
              }
          )
          .then(function(response) {
              if (response.status == 200) {
                  switch (response.data.Result) {
                      case "A": // Approved
                          type = "success";
                          title = $translate.instant("GETCAESUCCESSTITLE");
                          message = $translate.instant("GETCAESUCCESSTITLE").toString().replace("{0}", response.data.CAE);                          
                          $window.open("/invoices" + response.data.ReportUrl, "_blank"); // Open invoice in new tab
                          break;
                      case "R": // Rejected
                          type = "warning";
                          title = $translate.instant("MODALALERTTITLE");
                          message = (response.data.CAE) ? $translate.instant("GETCAEERROR1") : $translate.instant("GETCAEERROR2");
                          $rootScope.hasCaeError = true;
                          break;
                      case "E": //The order is not valid to send
                          type = "warning";
                          title = $translate.instant("MODALALERTTITLE");
                          message = response.data.ValidationMessage;
                          $rootScope.hasCaeError = true;
                          break;
                  }

                  $scope.result = response.data.Result;
              } else {
                  // Unhandled exception
                  type = "error";
                  title = "Error";
                  message = "Ocurrió un error al solicitar el CAE.";
                  $rootScope.hasCaeError = true;
              }

              toaster.pop({
                  type: type,
                  title: title,
                  body: message,
                  showCloseButton: true,
                  progressBar: (type !== "success") ? true : false
              });
          })
          .catch(function() {
              toaster.pop({
                  type: "error",
                  title: "Error",
                  body: "Ocurrió un error inesperado al solicitar el CAE.",
                  showCloseButton: true,
                  progressBar: true
              });
              $scope.result = "E";
          })
          .finally(function() {
              $scope.submitting = false;
              $rootScope.modalInstance.close();

              //if we get a CAE number, we must redirected to the grid
              if ($scope.result === "A") {
                  $location.path("/index/orders-without-cae");
              }

              $rootScope.refreshList();
          });
    };

    
    /**
      * @ngdoc method
      * @name modalCaeCtrl#isEmpty
      *
      * @methodOf
      * inspinia.controller:modalCaeCtrl
      *
      * @description
      * determines if a value is null, empty or undefined
      *
      * @param {object} value value 
      * @return {bool} true if is empty, null or undefined. False otherwise
    */
    function isEmpty(value) {
        if ((value === "") | (value === null) | (value === undefined)) {
            return true;
        } else {
            return false;
        }
    }

    /**
      * @ngdoc method
      * @name modalCaeCtrl#_FUNCTION_NAME_
      *
      * @methodOf
      * inspinia.controller:modalCaeCtrl
      *
      * @description
      * validates if the exchange rate is numeric
      *
      * @param {string} value exchange rate value
      * @return {bool} true if is numeric. False otherwise
    */
    function isNumericExchangeRate(value) {
        if (typeof value !== undefined && typeof value === "number") {
            return true;
        } else {
            return false;
        }
    }

    /**
      * @ngdoc method
      * @name modalCaeCtrl#listEmails
      *
      * @methodOf
      * inspinia.controller:modalCaeCtrl
      *
      * @description
      * list client emails by client code
      *
      * @param {string} clienCode client code      
    */
    function listEmails(clientCode) {
        $http
            .get(ngAuthSettings.apiServiceBaseUri + "api/v1/Client/ListEmails", {
                params: {
                    clientId: clientCode
                },
                headers: {
                    'Cache-Control': 'no-cache'
                }
            })
            .then(function(response) {
                $scope.emails = response.data;
            });
    }


    /**
      * @ngdoc method
      * @name modalCaeCtrl#listDeliveryAddresses
      *
      * @methodOf
      * inspinia.controller:modalCaeCtrl
      *
      * @description
      * list delivery address
      *
      * @param {string} clientCode client code
      * @param {string} orderNumber order number
      * @param {string} orderType order type      
    */
    function listDeliveryAddresses(clientCode, orderNumber, orderType) {
        $http
            .get(
                ngAuthSettings.apiServiceBaseUri +
                "api/v1/Client/ListDeliveryAddresses", {
                    params: {
                        clientCode: clientCode,
                        orderNumber: orderNumber,
                        orderType: orderType
                    },
                    headers: {
                        'Cache-Control': 'no-cache'
                    }
                }
            )
            .then(function(response) {
                $scope.deliveryAddresses = response.data;
                // Set default
                for (var i = 0; i < $scope.deliveryAddresses.length; i++) {
                    if ($scope.deliveryAddresses[i].Origin === "Orden") {
                        $scope.deliveryAddressId = $scope.deliveryAddresses[i].Identifier;
                    }
                }
            });
    }

    $scope.init();
}

angular.module("inspinia").controller("modalCaeCtrl", modalCaeCtrl);