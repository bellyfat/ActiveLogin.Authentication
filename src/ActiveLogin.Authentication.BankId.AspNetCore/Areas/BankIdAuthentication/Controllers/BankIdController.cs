using System;
using ActiveLogin.Authentication.BankId.Api.UserMessage;
using ActiveLogin.Authentication.BankId.AspNetCore.Areas.BankIdAuthentication.Models;
using ActiveLogin.Authentication.BankId.AspNetCore.DataProtection;
using ActiveLogin.Authentication.BankId.AspNetCore.Models;
using ActiveLogin.Authentication.BankId.AspNetCore.UserMessage;
using ActiveLogin.Authentication.Common.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace ActiveLogin.Authentication.BankId.AspNetCore.Areas.BankIdAuthentication.Controllers
{
    [Area(BankIdConstants.AreaName)]
    [Route("/[area]/[action]")]
    [AllowAnonymous]
    public class BankIdController : Controller
    {
        private readonly IAntiforgery _antiforgery;
        private readonly IBankIdUserMessageLocalizer _bankIdUserMessageLocalizer;
        private readonly IBankIdLoginOptionsProtector _loginOptionsProtector;
        private readonly IStringLocalizer<BankIdHandler> _localizer;

        public BankIdController(
            IAntiforgery antiforgery,
            IBankIdUserMessageLocalizer bankIdUserMessageLocalizer,
            IBankIdLoginOptionsProtector loginOptionsProtector,
            IStringLocalizer<BankIdHandler> localizer)
        {
            _antiforgery = antiforgery;
            _bankIdUserMessageLocalizer = bankIdUserMessageLocalizer;
            _loginOptionsProtector = loginOptionsProtector;
            _localizer = localizer;
        }

        [HttpGet]
        public ActionResult Login(string returnUrl, string loginOptions)
        {
            if (!Url.IsLocalUrl(returnUrl))
            {
                throw new Exception(BankIdConstants.InvalidReturnUrlErrorMessage);
            }

            var unprotectedLoginOptions = _loginOptionsProtector.Unprotect(loginOptions);
            var antiforgeryTokens = _antiforgery.GetAndStoreTokens(HttpContext);

            var viewModel = GetLoginViewModel(returnUrl, loginOptions, unprotectedLoginOptions, antiforgeryTokens);
            return View(viewModel);
        }

        private BankIdLoginViewModel GetLoginViewModel(string returnUrl, string loginOptions, BankIdLoginOptions unprotectedLoginOptions, AntiforgeryTokenSet antiforgeryTokens)
        {
            var initialStatusMessage = GetInitialStatusMessage(unprotectedLoginOptions);
            var loginScriptOptions = new BankIdLoginScriptOptions(
                Url.Action("Initialize", "BankIdApi"),
                Url.Action("Status", "BankIdApi"),
                Url.Action("Cancel", "BankIdApi")
                )
            {
                RefreshIntervalMs = BankIdDefaults.StatusRefreshIntervalMs,

                InitialStatusMessage = _bankIdUserMessageLocalizer.GetLocalizedString(initialStatusMessage),
                UnknownErrorMessage = _bankIdUserMessageLocalizer.GetLocalizedString(MessageShortName.RFA22),

                UnsupportedBrowserErrorMessage = _localizer["UnsupportedBrowser_ErrorMessage"]
            };

            return new BankIdLoginViewModel(
                returnUrl,
                Url.Content(unprotectedLoginOptions.CancelReturnUrl),
                unprotectedLoginOptions.IsAutoLogin(),
                unprotectedLoginOptions.PersonalIdentityNumber?.To12DigitString() ?? string.Empty,
                loginOptions,
                unprotectedLoginOptions,
                loginScriptOptions,
                SystemRuntimeJsonSerializer.Serialize(loginScriptOptions),
                antiforgeryTokens.RequestToken
            );
        }

        private static MessageShortName GetInitialStatusMessage(BankIdLoginOptions loginOptions)
        {
            if (loginOptions.SameDevice)
            {
                return MessageShortName.RFA13;
            }

            if (loginOptions.UseQrCode)
            {
                return MessageShortName.RFA1QR;
            }

            return MessageShortName.RFA1;
        }
    }
}
