﻿// -----------------------------------------------------------------------
// <copyright file="ClientFactory.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.Store.PartnerCenter.PowerShell.Factories
{
    using System;
    using Authentication;
    using Common;

    /// <summary>
    /// Factory that provides initialized clients used to interact with online services.
    /// </summary>
    public class ClientFactory : IClientFactory
    {
        /// <summary>
        /// Creates a new instance of the object used to interface with Partner Center.
        /// </summary>
        /// <param name="context">The partner's execution context.</param>
        /// <param name="debugAction">The action to write debug statements.</param>
        /// <returns>An instance of the <see cref="PartnerOperations" /> class.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="context" /> is null.
        /// or 
        /// <paramref name="debugAction" /> is null.
        /// </exception>
        public virtual IPartner CreatePartnerOperations(PartnerContext context, Action<string> debugAction)
        {
            context.AssertNotNull(nameof(context));
            debugAction.AssertNotNull(nameof(debugAction));

            return PartnerService.Instance.CreatePartnerOperations(
                new PowerShellCredentials(
                    PartnerSession.Instance.AuthenticationFactory.Authenticate(context, debugAction)));
        }
    }
}