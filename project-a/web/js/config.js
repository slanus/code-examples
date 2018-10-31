function config(
    $stateProvider,
    $urlRouterProvider,
    $ocLazyLoadProvider,
    $provide,
    $httpProvider
  ) {
    $httpProvider.interceptors.push("authInterceptorService");
  
    $urlRouterProvider.otherwise("/login");
  
    $ocLazyLoadProvider.config({
      // Set to true if you want to see what and when is dynamically loaded
      debug: false
    });
  
    $stateProvider
  
      .state("index", {
        abstract: true,
        url: "/index",
        templateUrl: "views/common/content.html?v=104"
      })
      .state("index.main", {
        url: "/main",
        templateUrl: "views/main.html",
        data: { pageTitle: "Bienvenido" }
      })
      .state("login", {
        url: "/login",
        templateUrl: "views/login.html",
        data: { pageTitle: "Login", specialClass: "gray-bg" },
        resolve: {
          loadPlugin: function($ocLazyLoad) {
            return $ocLazyLoad.load([
              {
                serie: true,
                files: ["js/angular/angular-local-storage.min.js"]
              }
            ]);
          }
        }
      })

      // Quotations List
      .state("index.quotations", {
        url: "/quotations",
        templateUrl: "views/quotations/index.html",
        cache: false,
        data: { pageTitle: "Quotations" },
        resolve: {
          loadPlugin: function($ocLazyLoad) {
            return $ocLazyLoad.load([
              {
                serie: true,
                files: [
                  "js/plugins/dataTables/datatables.min.js",
                  "css/plugins/dataTables/datatables.min.css"
                ]
              },
              {
                serie: true,
                name: "datatables",
                files: ["js/plugins/dataTables/angular-datatables.min.js"]
              },
              {
                serie: true,
                name: "datatables.buttons",
                files: ["js/plugins/dataTables/angular-datatables.buttons.min.js"]
              },
              {
                serie: true,
                files: ["js/plugins/dataTables/angular-datatables-languages.js"]
              },
              {
                serie: true,
                files: [
                  "js/plugins/dataTables/dataTables.responsive.js",
                  "css/plugins/dataTables/dataTables.responsive.css"
                ]
              },
              {
                serie: true,
                files: ["js/plugins/moment/moment.min.js"]
              },
              {
                serie: true,
                name: "datePicker",
                files: [
                  "css/plugins/datapicker/angular-datapicker.css",
                  "js/plugins/datapicker/angular-datepicker.js"
                ]
              },
              {
                insertBefore: "#loadBefore",
                serie: true,
                name: "localytics.directives",
                files: [
                  "css/plugins/chosen/bootstrap-chosen.css",
                  "js/plugins/chosen/chosen.jquery.js",
                  "js/plugins/chosen/chosen.js"
                ]
              },
              {
                serie: true,
                files: [
                  "css/plugins/awesome-bootstrap-checkbox/awesome-bootstrap-checkbox.css"
                ]
              },
              {
                serie: true,
                name: "angular-ladda",
                files: [
                  "js/plugins/ladda/spin.min.js",
                  "js/plugins/ladda/ladda.min.js",
                  "css/plugins/ladda/ladda-themeless.min.css",
                  "js/plugins/ladda/angular-ladda.min.js"
                ]
              }
            ]);
          }
        }
      })

      // Quotations detail
      .state("index.quotation-detail", {
        url: "/quotation-detail/:orderType/:orderNumber",
        templateUrl: "views/quotations/detail.html",
        cache: false,
        data: { pageTitle: "Quotation Detail" },
        resolve: {
          loadPlugin: function($ocLazyLoad) {
            return $ocLazyLoad.load([
              {
                serie: true,
                files: [
                  "js/plugins/dataTables/datatables.min.js",
                  "css/plugins/dataTables/datatables.min.css"
                ]
              },
              {
                serie: true,
                name: "datatables",
                files: ["js/plugins/dataTables/angular-datatables.min.js"]
              },
              {
                serie: true,
                name: "datatables.buttons",
                files: ["js/plugins/dataTables/angular-datatables.buttons.min.js"]
              },
              {
                serie: true,
                files: ["js/plugins/dataTables/angular-datatables-languages.js"]
              },
              {
                serie: true,
                files: [
                  "js/plugins/dataTables/dataTables.responsive.js",
                  "css/plugins/dataTables/dataTables.responsive.css"
                ]
              },
              {
                serie: true,
                files: ["js/plugins/moment/moment.min.js"]
              },
              {
                serie: true,
                name: "datePicker",
                files: [
                  "css/plugins/datapicker/angular-datapicker.css",
                  "js/plugins/datapicker/angular-datepicker.js"
                ]
              },
              {
                insertBefore: "#loadBefore",
                serie: true,
                name: "localytics.directives",
                files: [
                  "css/plugins/chosen/bootstrap-chosen.css",
                  "js/plugins/chosen/chosen.jquery.js",
                  "js/plugins/chosen/chosen.js"
                ]
              },
              {
                serie: true,
                name: "angular-ladda",
                files: [
                  "js/plugins/ladda/spin.min.js",
                  "js/plugins/ladda/ladda.min.js",
                  "css/plugins/ladda/ladda-themeless.min.css",
                  "js/plugins/ladda/angular-ladda.min.js"
                ]
              }
            ]);
          }
        }
      })
  
      // Orders Without CAE
      .state("index.orders-without-cae", {
        url: "/orders-without-cae",
        templateUrl: "views/orderswithoutcae/index.html",
        cache: false,
        data: { pageTitle: "Orders" },
        resolve: {
          loadPlugin: function($ocLazyLoad) {
            return $ocLazyLoad.load([
              {
                serie: true,
                files: [
                  "js/plugins/dataTables/datatables.min.js",
                  "css/plugins/dataTables/datatables.min.css"
                ]
              },
              {
                serie: true,
                name: "datatables",
                files: ["js/plugins/dataTables/angular-datatables.min.js"]
              },
              {
                serie: true,
                name: "datatables.buttons",
                files: ["js/plugins/dataTables/angular-datatables.buttons.min.js"]
              },
              {
                serie: true,
                files: ["js/plugins/dataTables/angular-datatables-languages.js"]
              },
              {
                serie: true,
                files: [
                  "js/plugins/dataTables/dataTables.responsive.js",
                  "css/plugins/dataTables/dataTables.responsive.css"
                ]
              },
              {
                serie: true,
                files: ["js/plugins/moment/moment.min.js"]
              },
              {
                serie: true,
                name: "datePicker",
                files: [
                  "css/plugins/datapicker/angular-datapicker.css",
                  "js/plugins/datapicker/angular-datepicker.js"
                ]
              },
              {
                insertBefore: "#loadBefore",
                serie: true,
                name: "localytics.directives",
                files: [
                  "css/plugins/chosen/bootstrap-chosen.css",
                  "js/plugins/chosen/chosen.jquery.js",
                  "js/plugins/chosen/chosen.js"
                ]
              },
              {
                serie: true,
                files: [
                  "css/plugins/awesome-bootstrap-checkbox/awesome-bootstrap-checkbox.css"
                ]
              },
              {
                serie: true,
                name: "angular-ladda",
                files: [
                  "js/plugins/ladda/spin.min.js",
                  "js/plugins/ladda/ladda.min.js",
                  "css/plugins/ladda/ladda-themeless.min.css",
                  "js/plugins/ladda/angular-ladda.min.js"
                ]
              }
            ]);
          }
        }
      })

      //Details for Orders without CAE
      .state("index.order-detail-without-cae", {
        url: "/order-detail-without-cae/:orderType/:receiptLetter/:orderNumber/:deliveryNoteNumber",
        templateUrl: "views/orderswithoutcae/detail.html",
        cache: false,
        data: { pageTitle: "Order Detail" },
        resolve: {
          loadPlugin: function($ocLazyLoad) {
            return $ocLazyLoad.load([
              {
                serie: true,
                files: [
                  "js/plugins/dataTables/datatables.min.js",
                  "css/plugins/dataTables/datatables.min.css"
                ]
              },
              {
                serie: true,
                name: "datatables",
                files: ["js/plugins/dataTables/angular-datatables.min.js"]
              },
              {
                serie: true,
                name: "datatables.buttons",
                files: ["js/plugins/dataTables/angular-datatables.buttons.min.js"]
              },
              {
                serie: true,
                files: ["js/plugins/dataTables/angular-datatables-languages.js"]
              },
              {
                serie: true,
                files: [
                  "js/plugins/dataTables/dataTables.responsive.js",
                  "css/plugins/dataTables/dataTables.responsive.css"
                ]
              },
              {
                serie: true,
                files: ["js/plugins/moment/moment.min.js"]
              },
              {
                serie: true,
                name: "datePicker",
                files: [
                  "css/plugins/datapicker/angular-datapicker.css",
                  "js/plugins/datapicker/angular-datepicker.js"
                ]
              },
              {
                insertBefore: "#loadBefore",
                serie: true,
                name: "localytics.directives",
                files: [
                  "css/plugins/chosen/bootstrap-chosen.css",
                  "js/plugins/chosen/chosen.jquery.js",
                  "js/plugins/chosen/chosen.js"
                ]
              },
              {
                serie: true,
                name: "angular-ladda",
                files: [
                  "js/plugins/ladda/spin.min.js",
                  "js/plugins/ladda/ladda.min.js",
                  "css/plugins/ladda/ladda-themeless.min.css",
                  "js/plugins/ladda/angular-ladda.min.js"
                ]
              }
            ]);
          }
        }
      })
  
      // Orders with CAE
  
      .state("index.orders-with-cae", {
        url: "/orders-with-cae",
        templateUrl: "views/orderswithcae/index.html",
        cache: false,
        data: { pageTitle: "Orders" },
        resolve: {
          loadPlugin: function($ocLazyLoad) {
            return $ocLazyLoad.load([
              {
                serie: true,
                files: [
                  "js/plugins/dataTables/datatables.min.js",
                  "css/plugins/dataTables/datatables.min.css"
                ]
              },
              {
                serie: true,
                name: "datatables",
                files: ["js/plugins/dataTables/angular-datatables.min.js"]
              },
              {
                serie: true,
                name: "datatables.buttons",
                files: ["js/plugins/dataTables/angular-datatables.buttons.min.js"]
              },
              {
                serie: true,
                files: ["js/plugins/dataTables/angular-datatables-languages.js"]
              },
              {
                serie: true,
                files: [
                  "js/plugins/dataTables/dataTables.responsive.js",
                  "css/plugins/dataTables/dataTables.responsive.css"
                ]
              },
              {
                serie: true,
                files: ["js/plugins/moment/moment.min.js"]
              },
              {
                serie: true,
                name: "datePicker",
                files: [
                  "css/plugins/datapicker/angular-datapicker.css",
                  "js/plugins/datapicker/angular-datepicker.js"
                ]
              },
              {
                insertBefore: "#loadBefore",
                serie: true,
                name: "localytics.directives",
                files: [
                  "css/plugins/chosen/bootstrap-chosen.css",
                  "js/plugins/chosen/chosen.jquery.js",
                  "js/plugins/chosen/chosen.js"
                ]
              },
              {
                serie: true,
                files: [
                  "css/plugins/awesome-bootstrap-checkbox/awesome-bootstrap-checkbox.css"
                ]
              },
              {
                serie: true,
                name: "angular-ladda",
                files: [
                  "js/plugins/ladda/spin.min.js",
                  "js/plugins/ladda/ladda.min.js",
                  "css/plugins/ladda/ladda-themeless.min.css",
                  "js/plugins/ladda/angular-ladda.min.js"
                ]
              }
            ]);
          }
        }
      })
  
      // Orders with CAE Detail
      .state("index.order-detail-with-cae", {
        url: "/order-detail-with-cae/:orderType/:receiptType/:receiptNumber",
        params: {
          orderType: "",
          receiptType: "",
          receiptNumber: ""
        },
        templateUrl: "views/orderswithcae/detail.html",
        cache: false,
        data: { pageTitle: "Order Detail" },
        resolve: {
          data: ["$stateParams", function($stateParams) {}],
          loadPlugin: function($ocLazyLoad) {
            return $ocLazyLoad.load([
              {
                serie: true,
                files: [
                  "js/plugins/dataTables/datatables.min.js",
                  "css/plugins/dataTables/datatables.min.css"
                ]
              },
              {
                serie: true,
                name: "datatables",
                files: ["js/plugins/dataTables/angular-datatables.min.js"]
              },
              {
                serie: true,
                name: "datatables.buttons",
                files: ["js/plugins/dataTables/angular-datatables.buttons.min.js"]
              },
              {
                serie: true,
                files: ["js/plugins/dataTables/angular-datatables-languages.js"]
              },
              {
                serie: true,
                files: [
                  "js/plugins/dataTables/dataTables.responsive.js",
                  "css/plugins/dataTables/dataTables.responsive.css"
                ]
              },
              {
                serie: true,
                files: ["js/plugins/moment/moment.min.js"]
              },
              {
                serie: true,
                name: "datePicker",
                files: [
                  "css/plugins/datapicker/angular-datapicker.css",
                  "js/plugins/datapicker/angular-datepicker.js"
                ]
              },
              {
                insertBefore: "#loadBefore",
                serie: true,
                name: "localytics.directives",
                files: [
                  "css/plugins/chosen/bootstrap-chosen.css",
                  "js/plugins/chosen/chosen.jquery.js",
                  "js/plugins/chosen/chosen.js"
                ]
              }
            ]);
          }
        }
      })

      // Profile

      .state("index.change-password", {
        url: "/change-password",
        templateUrl: "views/profile/change-password.html",
        cache: false,
        data: { pageTitle: "Cambiar Contraseña" },
        resolve: {
          loadPlugin: function($ocLazyLoad) {
            return $ocLazyLoad.load([             
              {
                serie: true,
                name: "angular-ladda",
                files: [
                  "js/plugins/ladda/spin.min.js",
                  "js/plugins/ladda/ladda.min.js",
                  "css/plugins/ladda/ladda-themeless.min.css",
                  "js/plugins/ladda/angular-ladda.min.js"
                ]
              }
            ]);
          }
        }
      })

      // Users List

      .state("index.users-list", {
        url: "/users-list",
        templateUrl: "views/admin/users/index.html",
        cache: false,
        data: { pageTitle: "Listado de Usuarios" },
        resolve: {
          loadPlugin: function($ocLazyLoad) {
            return $ocLazyLoad.load([
              {
                serie: true,
                files: [
                  "js/plugins/dataTables/datatables.min.js",
                  "css/plugins/dataTables/datatables.min.css"
                ]
              },
              {
                serie: true,
                name: "datatables",
                files: ["js/plugins/dataTables/angular-datatables.min.js"]
              },
              {
                serie: true,
                name: "datatables.buttons",
                files: ["js/plugins/dataTables/angular-datatables.buttons.min.js"]
              },
              {
                serie: true,
                files: ["js/plugins/dataTables/angular-datatables-languages.js"]
              },
              {
                serie: true,
                files: [
                  "js/plugins/dataTables/dataTables.responsive.js",
                  "css/plugins/dataTables/dataTables.responsive.css"
                ]
              },
              {
                serie: true,
                files: ["js/plugins/moment/moment.min.js"]
              },
              {
                serie: true,
                name: "datePicker",
                files: [
                  "css/plugins/datapicker/angular-datapicker.css",
                  "js/plugins/datapicker/angular-datepicker.js"
                ]
              },
              {
                insertBefore: "#loadBefore",
                serie: true,
                name: "localytics.directives",
                files: [
                  "css/plugins/chosen/bootstrap-chosen.css",
                  "js/plugins/chosen/chosen.jquery.js",
                  "js/plugins/chosen/chosen.js"
                ]
              },
              {
                serie: true,
                files: [
                  "css/plugins/awesome-bootstrap-checkbox/awesome-bootstrap-checkbox.css"
                ]
              },
              {
                serie: true,
                name: "angular-ladda",
                files: [
                  "js/plugins/ladda/spin.min.js",
                  "js/plugins/ladda/ladda.min.js",
                  "css/plugins/ladda/ladda-themeless.min.css",
                  "js/plugins/ladda/angular-ladda.min.js"
                ]
              }
            ]);
          }
        }
      })

      // Edit user

      .state("index.edit-user", {
        url: "/edit-user/:userId",
        templateUrl: "views/admin/users/edit.html",
        cache: false,
        data: { pageTitle: "Edición de Usuario" },
        resolve: {
          loadPlugin: function($ocLazyLoad) {
            return $ocLazyLoad.load([
              {
                files: [
                  'js/plugins/dualListbox/jquery.bootstrap-duallistbox.js',
                  'css/plugins/dualListbox/bootstrap-duallistbox.min.css'
                ]
              },
              { 
                name: 'frapontillo.bootstrap-duallistbox',
                files: [
                  'js/plugins/dualListbox/angular-bootstrap-duallistbox.js'
                ]
              },
              {
                serie: true,
                name: "angular-ladda",
                files: [
                  "js/plugins/ladda/spin.min.js",
                  "js/plugins/ladda/ladda.min.js",
                  "css/plugins/ladda/ladda-themeless.min.css",
                  "js/plugins/ladda/angular-ladda.min.js"
                ]
              }
            ]);
          }
        }
      });  
  }
  angular
    .module("inspinia")
    .config(config)
    .run(function($rootScope, $state, authService) {
      $rootScope.$state = $state;
      authService.fillAuthData();
    });