﻿/*
 * Copyright (c) Dominick Baier.  All rights reserved.
 * see license.txt
 */

using System.ComponentModel.Composition;
using System.IdentityModel.Services;
using System.Security.Claims;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using Thinktecture.IdentityServer.Repositories;
using Thinktecture.IdentityServer.TokenService;
using Thinktecture.IdentityServer.Web.Security;

namespace Thinktecture.IdentityServer.Protocols.WSFederation
{
    [ClaimsAuthorize(Constants.Actions.Issue, Constants.Resources.WSFederation)]
    public class WSFederationController : Controller
    {
        [Import]
        public IConfigurationRepository ConfigurationRepository { get; set; }

        public WSFederationController()
        {
            Container.Current.SatisfyImportsOnce(this);
        }

        public WSFederationController(IConfigurationRepository configurationRepository)
        {
            ConfigurationRepository = configurationRepository;
        }

        public ActionResult Issue()
        {
            Tracing.Verbose("WS-Federation endpoint called.");

            if (!ConfigurationRepository.WSFederation.Enabled && ConfigurationRepository.WSFederation.EnableAuthentication)
            {
                return new HttpNotFoundResult();
            }

            var message = WSFederationMessage.CreateFromUri(HttpContext.Request.Url);

            // sign in 
            var signinMessage = message as SignInRequestMessage;
            if (signinMessage != null)
            {
                return ProcessWSFederationSignIn(signinMessage, HttpContext.User);
            }

            // sign out
            var signoutMessage = message as SignOutRequestMessage;
            if (signoutMessage != null)
            {
                return ProcessWSFederationSignOut(signoutMessage);
            }

            return View("Error");
        }

        #region Helper
        private ActionResult ProcessWSFederationSignIn(SignInRequestMessage message, IPrincipal principal)
        {
            // issue token and create ws-fed response
            var response = FederatedPassiveSecurityTokenServiceOperations.ProcessSignInRequest(
                message,
                principal as ClaimsPrincipal,
                TokenServiceConfiguration.Current.CreateSecurityTokenService());

            // set cookie for single-sign-out
            new SignInSessionsManager(HttpContext, ConfigurationRepository.Global.MaximumTokenLifetime)
                .AddRealm(response.BaseUri.AbsoluteUri);

            return new WSFederationResult(response, requireSsl: ConfigurationRepository.WSFederation.RequireSslForReplyTo);
        }

        private ActionResult ProcessWSFederationSignOut(SignOutRequestMessage message)
        {
            FederatedAuthentication.SessionAuthenticationModule.SignOut();

            // check for return url
            if (!string.IsNullOrWhiteSpace(message.Reply))
            {
                ViewBag.ReturnUrl = message.Reply;
            }

            // check for existing sign in sessions
            var mgr = new SignInSessionsManager(HttpContext);
            var realms = mgr.GetRealms();
            mgr.ClearRealms();
            
            return View("Signout", realms);
        }
        #endregion
    }
}