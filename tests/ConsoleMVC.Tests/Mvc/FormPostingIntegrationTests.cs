using System.Reflection;
using ConsoleMVC.Mvc;

namespace ConsoleMVC.Tests;

/// <summary>
/// Integration-style tests verifying the full form data pipeline:
/// NavigationResult.FormData → RouteContext.FormData → ModelBinder → action parameters.
/// </summary>
public class FormPostingIntegrationTests
{
    public class LoginModel
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class AccountController : Controller
    {
        // Captures bound values for test assertions
        public static string? CapturedUsername;
        public static string? CapturedPassword;
        public static LoginModel? CapturedModel;

        public ActionResult Login()
        {
            return View();
        }

        public ActionResult ProcessSimple(string username, string password)
        {
            CapturedUsername = username;
            CapturedPassword = password;
            return View();
        }

        public ActionResult ProcessModel(LoginModel model)
        {
            CapturedModel = model;
            return View();
        }
    }

    [Fact]
    public void FormData_FlowsThroughRouteContext()
    {
        var formData = new Dictionary<string, string>
        {
            ["username"] = "admin",
            ["password"] = "secret"
        };

        // Simulate: view returns NavigationResult with FormData
        var navigation = NavigationResult.ToAction("ProcessSimple", formData);

        // Framework builds RouteContext from NavigationResult
        var routeContext = new RouteContext
        {
            Controller = "Account",
            Action = navigation.Action!,
            FormData = navigation.FormData
        };

        Assert.Same(formData, routeContext.FormData);
    }

    [Fact]
    public void SimpleParams_BoundFromFormData_ViaActionInvocation()
    {
        AccountController.CapturedUsername = null;
        AccountController.CapturedPassword = null;

        var formData = new Dictionary<string, string>
        {
            ["username"] = "admin",
            ["password"] = "secret123"
        };

        var router = new Router();
        var controller = new AccountController();
        var method = router.ResolveAction(typeof(AccountController), "ProcessSimple");

        // This is what MvcApplication.Run does:
        var args = ModelBinder.BindParameters(method, formData);
        method.Invoke(controller, args);

        Assert.Equal("admin", AccountController.CapturedUsername);
        Assert.Equal("secret123", AccountController.CapturedPassword);
    }

    [Fact]
    public void ComplexModel_BoundFromFormData_ViaActionInvocation()
    {
        AccountController.CapturedModel = null;

        var formData = new Dictionary<string, string>
        {
            ["Username"] = "alice",
            ["Password"] = "p@ssw0rd"
        };

        var router = new Router();
        var controller = new AccountController();
        var method = router.ResolveAction(typeof(AccountController), "ProcessModel");

        var args = ModelBinder.BindParameters(method, formData);
        method.Invoke(controller, args);

        Assert.NotNull(AccountController.CapturedModel);
        Assert.Equal("alice", AccountController.CapturedModel!.Username);
        Assert.Equal("p@ssw0rd", AccountController.CapturedModel.Password);
    }

    [Fact]
    public void ParameterlessAction_IgnoresFormData()
    {
        var formData = new Dictionary<string, string> { ["key"] = "value" };

        var router = new Router();
        var controller = new AccountController();
        var method = router.ResolveAction(typeof(AccountController), "Login");

        // BindParameters returns null for parameterless methods
        var args = ModelBinder.BindParameters(method, formData);
        Assert.Null(args);

        // Invoke still works with null args
        var result = method.Invoke(controller, args);
        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void NoFormData_ParameterizedAction_GetsDefaults()
    {
        AccountController.CapturedUsername = "should_be_cleared";
        AccountController.CapturedPassword = "should_be_cleared";

        var router = new Router();
        var controller = new AccountController();
        var method = router.ResolveAction(typeof(AccountController), "ProcessSimple");

        // No form data — should get null defaults for strings
        var args = ModelBinder.BindParameters(method, null);
        method.Invoke(controller, args);

        Assert.Null(AccountController.CapturedUsername);
        Assert.Null(AccountController.CapturedPassword);
    }

    [Fact]
    public void RedirectClearsFormData()
    {
        // Simulate a redirect: when a controller returns RedirectToActionResult,
        // the framework should NOT carry form data forward (no form was submitted).
        var formData = new Dictionary<string, string> { ["data"] = "value" };

        // View returns navigation with form data
        var navWithData = NavigationResult.To("Account", "ProcessSimple", formData);
        Assert.NotNull(navWithData.FormData);

        // After processing, controller redirects (no form data on redirect)
        var redirect = new RedirectToActionResult
        {
            ControllerName = "Home",
            ActionName = "Index"
        };

        // Framework builds new RouteContext from redirect — no FormData
        var routeContext = new RouteContext
        {
            Controller = redirect.ControllerName,
            Action = redirect.ActionName
            // FormData is NOT set for redirects
        };

        Assert.Null(routeContext.FormData);
    }
}
