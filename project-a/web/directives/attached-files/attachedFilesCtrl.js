
/**
  * @this vm
  * @ngdoc controller
  * @name MODULE_NAME.controller:_CONTROLLER_NAME_
  *
  * @description
  * description
*/
function attachedFilesCtrl(
  $scope,
  $http,
  ngAuthSettings,
  uploadFileService
) {
  
  // Initialize scope variables
  $scope.files = [];
  $scope.loading = true;

  /**
    * @ngdoc method
    * @name attachedFilesCtrl#init
    *
    * @methodOf
    * inspinia.controller:attachedFilesCtrl
    *
    * @description
    * Initializes the directive loading all the files related to some order
    *
  */
  $scope.listFiles = function () {
    $scope.files = [];
    $scope.loading = true;

    $http
      .get(
        ngAuthSettings.apiServiceBaseUri +
        "api/v1/FileAttachment/ListFiles",
        {
          params: {
            orderNumber: $scope.orderNumber,
            orderType: $scope.orderType,
            source: $scope.source
          },
          headers: { 'Cache-Control': 'no-cache' }
        }
      )
      .then(function (response) {
        if (response.status === 200) {
          var data = response.data;
          for (var i = 0; i < data.length; i++) {
            var fileInfo = data[i];
            $scope.files.push({ name: fileInfo.Name, url: fileInfo.Url, selected: false });
          }
        }
      })
      .catch(function () {
        // Show error
      })
      .finally(function () {
        $scope.loading = false;
      });
  };
  
  /**
    * @ngdoc method
    * @name attachedFilesCtrl#uploadFile
    *
    * @methodOf
    * inspinia.controller:attachedFilesCtrl
    *
    * @description
    * Description
    *
  */
  $scope.uploadFile = function() {
      $scope.loading = true;

      //get the fileinput object
      var fileInput = document.getElementById("fileInput");
      
      //do nothing if there's no files
      if (fileInput.files.length === 0) return;

      //there is a file present
      var file = fileInput.files[0];

      var orderData = new FormData();
      orderData.append("orderNumber", $scope.orderNumber);
      orderData.append("orderType", $scope.orderType);
      orderData.append("source", $scope.source);
      orderData.append("file", file);
      
      // Upload file
      uploadFileService.uploadFile(ngAuthSettings.apiServiceBaseUri +
        "api/v1/FileAttachment/Upload", orderData).then(function(){
          // file upload success
          $scope.listFiles();
      }).catch(function() {
          // unexpected error
      }).finally(function(){        
        // Clear file
        var fileElement = angular.element('#fileInput');
        angular.element(fileElement).val(null);

        $scope.loading = false;
      });
  };


  /**
    * @ngdoc method
    * @name attachedFilesCtrl#_FUNCTION_NAME_
    *
    * @methodOf
    * inspinia.controller:attachedFilesCtrl
    *
    * @description
    * Description
    *
    * @param {obj} event event
    * @return {obj} args arguments
  */
  $scope.$on("seletedFile", function (event, args) {
    $scope.$apply(function () {
      // Validate if the file already exists in our file's array
      var exists = false;
      for (var i =0; i < $scope.files.length; i++) {
        if($scope.files[i].name === args.file.name) {
          exists = true;
        }
      }
       // Add the file object to the scope's files collection (only if it doesn't exist)
      if(!exists) {
        $scope.files.push(args.file);
      }
    });
  }); 

  // Initialize directive
  $scope.listFiles();
}

angular.module("inspinia").controller("attachedFilesCtrl", attachedFilesCtrl);