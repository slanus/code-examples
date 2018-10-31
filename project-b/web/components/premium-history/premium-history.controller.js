'use strict';

angular.module('inspinia')
  .controller('premiumHistoryController', function(policiesService, premiumService, borrowersService, $filter) {
    var ph = this;

    ph.showSpinner = true;

    ph.$onInit = function() {

      policiesService.getActualPolicy().subscribe(function(policy) {

        ph.showSpinner = true;

        if (policy) {

          policy.borrower = borrowersService.getCurrentBorrower();
          premiumService.getByPolicy(policy).then(function(premium) {
            var cv = new wijmo.collections.CollectionView($filter('orderBy')(premium, 'DATE_ADDL_PREM_PAID', true));
            ph.data = cv;
            ph.showSpinner = false;
          }, function(error) {
            console.log(error);
          });
        }
      });

    };

    ph.initGrid = function(sender, args) {
      var flex = sender;
      flex.columnFooters.rows.push(new wijmo.grid.GroupRow());
    };
  });
