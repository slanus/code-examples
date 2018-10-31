
/**
  * @this vm
  * @ngdoc controller
  * @name inspinia.controller:sendEmailCtrl
  *
  * @description
  * email management controller
*/
function sendEmailCtrl(
  $scope,
  $http,
  ngAuthSettings
) {
  
  $scope.addedEmail = "";  
  $scope.selectedEmails = []; 
  $scope.formatEmail = /^[a-z]+[a-z0-9._]+@[a-z0-9]([a-z0-9-]*[a-z0-9])?(\.[a-z0-9]([a-z0-9-]*[a-z0-9])?)*$/i; 

  $scope.addNew = function(value) {
    $scope.addNewEmail = value;
  };
}

angular.module("inspinia").controller("sendEmailCtrl", sendEmailCtrl);