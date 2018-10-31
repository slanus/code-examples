'use strict';

angular.module('inspinia')
    .factory('usersService', function (apiService) {

    	return {
    		
            getAll: function () {

    			return apiService
                    .get(['Users', 'GetAll'], true);
            },
            update: function (user) {

                return apiService
                    .get({
                        path: ['Users', 'Update'],
                        query: user                        
                    }, true);
            },
            changePassword: function (user) {

                return apiService
                    .post({
                        path: ['Users', 'ChangePassword'],
                        extOpts: user
                    });
            },
            create: function (user) {
                return apiService
                    .get({
                        path: ['Users', 'Create'],
                        query: user
                    }, true);                    
            },
            hasMultipleClients: function () {
                return apiService
                    .get(['Users', 'HasMultipleClients'], true);
            }
    	};

    });
