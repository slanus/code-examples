'use strict';

angular.module('inspinia')
  .component('loginHistory', {
    templateUrl: 'app/components/login-history/login-history.html',
    controller: 'loginHistoryController',
    controllerAs: 'lh',
    bindings: {
      companyId: '<'
    }
  });
