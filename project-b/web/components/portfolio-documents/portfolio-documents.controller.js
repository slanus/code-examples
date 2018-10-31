'use strict';

angular.module('inspinia')
  .controller('portfolioDocumentsController', function($scope,
    borrowersService,
    reportUtilService,
    documentsService) {

    var pd = this;

    pd.showSpinner = true;

    pd.$onInit = function() {

      pd.showSpinner = true;

      borrowersService.getActualBorrower().subscribe(function(borrower) {
        documentsService
          .GetDocumentsByCompanyUI(borrower)
          .then(function(data) {
            console.log('data', data);
            startUpGrid(data);
          }, function(error) {
            console.log(error);
          });
      });

    };

    function startUpGrid(data) {

      var formattedData = [];
      for (var i = 0; i < data.length; i++) {
        formattedData.push(data[i]);
        formattedData[i].SyncDate = new Date(data[i].SyncDate);
      }
      var cv = new wijmo.collections.CollectionView(formattedData);      
      pd.data = cv;
      pd.showSpinner = false;

      $scope.$watch('pd.filter', function(n, o) {

        reportUtilService.filterByInput({
          flexGridFilter: pd.flexGridFilter,
          flex: pd.flex,
          search: n
        });

      });
    }

    pd.initGrid = function(sender, args) {
      var flex = sender;

      pd.flexGridFilter = new wijmo.grid.filter.FlexGridFilter(flex);

      pd.flexGridFilter.filterChanged.addHandler(function(s) {
        pd.data.filter = null;
      });

    };

    pd.download = function(documentId) {

      documentsService.GetDocument({
        DocumentId: documentId,
        BorrowerId: borrowersService.getCurrentBorrower().BorrowerID
      }).then(function(data) {

        if (data.ExistsFile) {
          documentsService.Download(documentId).then(function(file) {

            var blob = new Blob([file], {
              type: data.DocType
            });
            var fileName = data.FileName;
            saveAs(blob, fileName);
          });

        } else {
          alert('File not found');
        }
      });

    }

  });
