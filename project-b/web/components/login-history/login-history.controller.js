'use strict';

angular.module('inspinia')
  .controller('loginHistoryController', function(logService) {

    var lh = this;

    lh.showSpinner = true;

    lh.$onInit = function() {

      lh.showSpinner = true;

      logService.getHistory(lh.companyId).then(function(data) {
        var formattedData = [];
        for (var i = 0; i < data.length; i++) {
          formattedData.push(data[i]);
          formattedData[i].LogDateLocalTime = new Date(data[i].LogDateLocalTime);
        }
        startUpGrid(formattedData);
      });
    };

    function startUpGrid(data) {
      var cv = new wijmo.collections.CollectionView(data);

      lh.data = cv;
      lh.showSpinner = false;
    }

    lh.initGrid = function(sender, args) {
      var flex = sender;

      lh.flexGridFilter = new wijmo.grid.filter.FlexGridFilter(flex);
    };


  });
