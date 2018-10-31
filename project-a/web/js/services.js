/**
 * formatNumber - service to format to number
 */

function formatNumber() {
  this.format = function format(value) {
    var intsep = ".";
    var decsep = ",";
    value += "";
    var splitStr = value.split(".");
    var splitLeft = splitStr[0];
    var splitRight = splitStr.length > 1 ? decsep + splitStr[1] : "";
    var regx = /(\d+)(\d{3})/;
    while (regx.test(splitLeft)) {
      splitLeft = splitLeft.replace(regx, "$1" + intsep + "$2");
    }
    value = splitLeft + splitRight;
    return value;
  };
}

/**
 * authService - service used for authentication
 */
function authService(
  $http,
  $q,
  localStorageService,
  ngAuthSettings,
  $location
) {
  var serviceBase = ngAuthSettings.apiServiceBaseUri;
  var authServiceFactory = {};

  var _authentication = {
    isAuth: false,
    userName: "",
    useRefreshTokens: false
  };

  var _saveRegistration = function(registration) {
    _logOut();

    return $http
      .post(serviceBase + "api/account/register", registration)
      .then(function(response) {
        return response;
      });
  };

  var _login = function(loginData) {
    var data =
      "grant_type=password&username=" +
      loginData.userName +
      "&password=" +
      loginData.password;

    if (loginData.useRefreshTokens) {
      data = data + "&client_id=" + ngAuthSettings.clientId;
    }

    var deferred = $q.defer();

    $http
      .post(serviceBase + "token", data, {
        headers: { "Content-Type": "application/x-www-form-urlencoded" }
      })
      .success(function(response) {        
        if (loginData.useRefreshTokens) {
          localStorageService.set("authorizationData", {
            token: response.access_token,
            userName: loginData.userName,
            refreshToken: response.refresh_token,
            useRefreshTokens: true
          });
        } else {
          localStorageService.set("authorizationData", {
            token: response.access_token,
            userName: loginData.userName,
            refreshToken: "",
            useRefreshTokens: false
          });
        }
        _authentication.isAuth = true;
        _authentication.userName = loginData.userName;
        _authentication.useRefreshTokens = loginData.useRefreshTokens;

        deferred.resolve(response);
      })
      .error(function(err, status) {
        _logOut();
        deferred.reject(err);
      });

    return deferred.promise;
  };

  var _logOut = function() {
    localStorageService.remove("authorizationData");

    _authentication.isAuth = false;
    _authentication.userName = "";
    _authentication.useRefreshTokens = false;
  };

  var _fillAuthData = function() {
    var authData = localStorageService.get("authorizationData");
    if (authData) {
      _authentication.isAuth = true;
      _authentication.userName = authData.userName;
      _authentication.useRefreshTokens = authData.useRefreshTokens;
    }
  };

  var _refreshToken = function() {
    var deferred = $q.defer();

    var authData = localStorageService.get("authorizationData");

    if (authData) {
      if (authData.useRefreshTokens) {
        var data =
          "grant_type=refresh_token&refresh_token=" +
          authData.refreshToken +
          "&client_id=" +
          ngAuthSettings.clientId;

        localStorageService.remove("authorizationData");

        $http
          .post(serviceBase + "token", data, {
            headers: { "Content-Type": "application/x-www-form-urlencoded" }
          })
          .success(function(response) {
            localStorageService.set("authorizationData", {
              token: response.access_token,
              userName: response.userName,
              refreshToken: response.refresh_token,
              useRefreshTokens: true
            });

            deferred.resolve(response);
          })
          .error(function(err, status) {
            _logOut();
            deferred.reject(err);
          });
      }
    }

    return deferred.promise;
  };

  var _obtainAccessToken = function(externalData) {
    var deferred = $q.defer();

    $http
      .get(serviceBase + "api/account/ObtainLocalAccessToken", {
        params: {
          provider: externalData.provider,
          externalAccessToken: externalData.externalAccessToken
        }
      })
      .success(function(response) {        
        localStorageService.set("authorizationData", {
          token: response.access_token,
          userName: response.userName,
          refreshToken: "",
          useRefreshTokens: false
        });

        _authentication.isAuth = true;
        _authentication.userName = response.userName;
        _authentication.useRefreshTokens = false;

        deferred.resolve(response);
      })
      .error(function(err, status) {
        _logOut();
        deferred.reject(err);
      });

    return deferred.promise;
  };

  var _getCurrentToken = function() {
    var authData = localStorageService.get("authorizationData");
    if (authData) {
      return authData.token;
    }
    return null;
  };

  var _HandleResponse = function(response) {
    if (response.status === 401) {
      var authData = localStorageService.get("authorizationData");
      if (authData) {
        if (authData.useRefreshTokens) {
          $location.path("/refresh");
        }
      }
      _logOut();
      var path = ngAuthSettings.siteBaseUri + "#/login";
      window.location.href = path;
    }
  };

  //   ValidateToken - returns true if it has a valid token and false if it does not have it, and redirects in both cases to the correct page
  var _validateToken = function() {
    var token = _getCurrentToken();
    // if it is in the system and does not have a token, it redirects to the login and returns false
    if (token === null) {
      $location.path("/login");
      return false;
    } else if (token !== null && $location.path() === "/login") {
      // if you have a token and you are logging in, redirect it to main.html and return true
      $location.path("/index/main");
      return true;
    } else if (token !== null) {
      // if you have a token and you aren't logging in , return true
      return true;
    }
  };

  authServiceFactory.saveRegistration = _saveRegistration;
  authServiceFactory.login = _login;
  authServiceFactory.logOut = _logOut;
  authServiceFactory.fillAuthData = _fillAuthData;
  authServiceFactory.authentication = _authentication;
  authServiceFactory.refreshToken = _refreshToken;
  authServiceFactory.getCurrentToken = _getCurrentToken;
  authServiceFactory.handleResponse = _HandleResponse;
  authServiceFactory.validateToken = _validateToken;

  authServiceFactory.obtainAccessToken = _obtainAccessToken;

  return authServiceFactory;
}

/**
 * tokensManagerService - service used for tokens management
 */
function tokensManagerService($http, ngAuthSettings) {
  var serviceBase = ngAuthSettings.apiServiceBaseUri;

  var tokenManagerServiceFactory = {};

  var _getRefreshTokens = function() {
    return $http.get(serviceBase + "api/refreshtokens").then(function(results) {
      return results;
    });
  };

  var _deleteRefreshTokens = function(tokenid) {
    return $http
      .delete(serviceBase + "api/refreshtokens/?tokenid=" + tokenid)
      .then(function(results) {
        return results;
      });
  };

  tokenManagerServiceFactory.deleteRefreshTokens = _deleteRefreshTokens;
  tokenManagerServiceFactory.getRefreshTokens = _getRefreshTokens;

  return tokenManagerServiceFactory;
}

/**
 * authInterceptorService - interceptor used to authenticate every request if the token is still valid.
 */
function authInterceptorService($q, $injector, $location, localStorageService) {
  var authInterceptorServiceFactory = {};

  var _request = function(config) {
    config.headers = config.headers || {};

    var authData = localStorageService.get("authorizationData");
    if (authData) {
      config.headers.Authorization = "Bearer " + authData.token;
    }

    return config;
  };

  var _responseError = function(rejection) {
    if (rejection.status === 401) {
      var authService = $injector.get("authService");
      var authData = localStorageService.get("authorizationData");

      if (authData) {
        if (authData.useRefreshTokens) {
          $location.path("/refresh");
          return $q.reject(rejection);
        }
      }
      authService.logOut();
      $location.path("/login");
    }
    return $q.reject(rejection);
  };

  authInterceptorServiceFactory.request = _request;
  authInterceptorServiceFactory.responseError = _responseError;

  return authInterceptorServiceFactory;
}

/**
 * uploadFileService - service used to upload attachments on any order
 */
function uploadFileService($http) {
  this.uploadFile = function(url, orderData) {
    return $http({
      url: url,
      method: 'POST',
      data: orderData,
      headers: { 'Content-Type': undefined },
      transformRequest: angular.identity
    });
  };
}

angular
  .module("inspinia")  
  .service("formatNumber", formatNumber)
  .service("authService", authService)
  .service("tokensManagerService", tokensManagerService)
  .service("authInterceptorService", authInterceptorService)
  .service("uploadFileService", uploadFileService);
