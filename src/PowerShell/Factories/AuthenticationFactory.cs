﻿// -----------------------------------------------------------------------
// <copyright file="AuthenticationFactory.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.Store.PartnerCenter.PowerShell.Factories
{
    using System;
    using System.Globalization;
    using System.IdentityModel.Tokens.Jwt;
    using System.Linq;
    using System.Security.Claims;
    using Authentication;
    using IdentityModel.Clients.ActiveDirectory;

    /// <summary>
    /// Factory used to perform authentication operations.
    /// </summary>
    public class AuthenticationFactory : IAuthenticationFactory
    {
        /// <summary>
        /// The value for the redirect URI.
        /// </summary>
        private const string redirectUriValue = "urn:ietf:wg:oauth:2.0:oob";

        /// <summary>
        /// Acquires the security token from the authority.
        /// </summary>
        /// <param name="context">Context to be used when requesting a security token.</param>
        /// <param name="debugAction">The action to write debug statements.</param>
        /// <param name="promptAction">The action to prompt the user for input.</param>
        /// <returns>The result from the authentication request.</returns>
        public AuthenticationToken Authenticate(PartnerContext context, Action<string> debugAction, Action<string> promptAction = null)
        {
            AuthenticationContext authContext;
            AuthenticationResult authResult;
            Claim claim;
            DateTimeOffset expiration;
            JwtSecurityToken token;
            JwtSecurityTokenHandler tokenHandler;
            PartnerEnvironment environment;

            environment = PartnerEnvironment.PublicEnvironments[context.Environment];

            authContext = new AuthenticationContext(
                $"{environment.ActiveDirectoryAuthority}{context.Account.Properties[AzureAccountPropertyType.Tenant]}");

            if (context.Account.Type == AccountType.AccessToken)
            {
                debugAction("Attempting to authenticate using an access token.");

                tokenHandler = new JwtSecurityTokenHandler();
                token = tokenHandler.ReadJwtToken(context.Account.Properties[AzureAccountPropertyType.AccessToken]);

                claim = token.Claims.SingleOrDefault(c => c.Type.Equals("oid", StringComparison.InvariantCultureIgnoreCase));
                context.Account.Properties[AzureAccountPropertyType.UserIdentifier] = claim?.Value;

                debugAction($"The object identifier {claim?.Value} was found in the claims associated with the token.");

                claim = token.Claims.SingleOrDefault(c => c.Type.Equals("tid", StringComparison.InvariantCultureIgnoreCase));
                context.Account.Properties[AzureAccountPropertyType.Tenant] = claim?.Value;

                debugAction($"The tenant identifier {claim?.Value} was found in the claims associated with the token.");

                claim = token.Claims.Single(c => c.Type.Equals("exp", StringComparison.InvariantCultureIgnoreCase));
                expiration = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(claim.Value, CultureInfo.InvariantCulture));

                debugAction($"The specified access token expires on {expiration.ToString(CultureInfo.CurrentCulture)}.");

                return new AuthenticationToken(
                    context.Account.Properties[AzureAccountPropertyType.AccessToken],
                    expiration);
            }
            else if (context.Account.Type == AccountType.ServicePrincipal)
            {
                debugAction("Attempting to authenticate using a service principal. Please note not all operations support this type of authentication.");

                authResult = authContext.AcquireTokenAsync(
                    environment.AzureAdGraphEndpoint,
                    new ClientCredential(
                        context.Account.Id,
                        context.Account.Properties[AzureAccountPropertyType.ServicePrincipalSecret])).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            else if (PartnerSession.Instance.Context == null)
            {
#if NETSTANDARD
                debugAction("Attempting to authenticate using the device code flow."); 

                DeviceCodeResult deviceCodeResult = authContext.AcquireDeviceCodeAsync(
                    environment.PartnerCenterEndpoint,
                    context.ApplicationId).ConfigureAwait(false).GetAwaiter().GetResult();

                promptAction(deviceCodeResult.Message);

                authResult = authContext.AcquireTokenByDeviceCodeAsync(deviceCodeResult).ConfigureAwait(false).GetAwaiter().GetResult();
#else
                debugAction("Attempting to authenticate by prompting for credentials.");

                authResult = authContext.AcquireTokenAsync(
                    environment.PartnerCenterEndpoint,
                    context.ApplicationId,
                    new Uri(redirectUriValue),
                    new PlatformParameters(PromptBehavior.Always),
                    UserIdentifier.AnyUser).ConfigureAwait(false).GetAwaiter().GetResult();
#endif

                context.Account.Id = authResult.UserInfo.DisplayableId;
                context.Account.Properties[AzureAccountPropertyType.Tenant] = authResult.TenantId;
                context.Account.Properties[AzureAccountPropertyType.UserIdentifier] = authResult.UserInfo.UniqueId;
            }
            else
            {
                authResult = authContext.AcquireTokenSilentAsync(
                    environment.PartnerCenterEndpoint,
                    context.ApplicationId,
                    new UserIdentifier(context.Account.Id, UserIdentifierType.RequiredDisplayableId)).ConfigureAwait(false).GetAwaiter().GetResult();

                context.Account.Id = authResult.UserInfo.DisplayableId;
                context.Account.Properties[AzureAccountPropertyType.Tenant] = authResult.TenantId;
                context.Account.Properties[AzureAccountPropertyType.UserIdentifier] = authResult.UserInfo.UniqueId;
            }

            return new AuthenticationToken(authResult.AccessToken, authResult.ExpiresOn);
        }
    }
}