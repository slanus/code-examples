'use strict';

angular.module('inspinia')
    .factory('premiumService', function (apiService) {

        return {
            getByPolicy: function (options) {

                return apiService
                    .get({
                        path: ['Premium', 'GetByPolicyUI'],
                        query: { BorrowerId: options.borrower.BorrowerID, PoliciesID: options.Id }
                    });
            },
            getCashValuesByPolicy: function (options) {

                return apiService
                    .get({
                        path: ['Premium', 'GetCashValuesByPolicy'],
                        query: { BorrowerId: options.borrower.BorrowerID, PoliciesID: options.Id }
                    });
            }
        };
    });