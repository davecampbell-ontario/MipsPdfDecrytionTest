/*
*
* Copyright (c) Microsoft Corporation.
* All rights reserved.
*
* This code is licensed under the MIT License.
*
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files(the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions :
*
* The above copyright notice and this permission notice shall be included in
* all copies or substantial portions of the Software.
*
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
* THE SOFTWARE.
*
*/

using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Identity.Client;
using Microsoft.InformationProtection;

namespace MipsTestApp.Models.Protection.File
{
    public class AuthDelegateUserImplementation(ApplicationInfo appInfo, IHttpContextAccessor httpContextAccessor, string DocSecret, string tenantId, TelemetryClient LoggingClient) : IAuthDelegate
    {
        private string tenant { get; init; } = tenantId;
        private string DocClientSecret { get; init; } = DocSecret;
        private UserAssertion Assertion { get; init; } = httpContextAccessor?.HttpContext is null ? null : new UserAssertion(httpContextAccessor.HttpContext.GetTokenAsync("access_token").Result, "urn:ietf:params:oauth:grant-type:jwt-bearer");
        private ApplicationInfo AppInfo { get; init; } = appInfo;
        public string AcquireToken(Identity identity, string authority, string resource, string claims)
        {
            LoggingClient.TrackTrace($"Try to aquireToken for user {identity.Name}", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Verbose);
            var authorityUri = new Uri(authority);
            authority = string.Format("https://{0}/{1}", authorityUri.Host, tenant);
            IConfidentialClientApplication ClientApp = ConfidentialClientApplicationBuilder.Create(AppInfo.ApplicationId)
                                                                                            .WithAuthority(authority)
                                                                                            .WithClientSecret(DocClientSecret) //Need to get instead of hardcode
                                                                                            .Build();

            // Append .default to the resource passed in to AcquireToken().
            string[] scopes = [resource[resource.Length - 1].Equals('/') ? $"{resource}.default" : $"{resource}/.default"];

            LoggingClient.TrackTrace($"Make call to {authorityUri} for scopes: {string.Join(", ", scopes)}", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Information);

            string newAccessToken = ClientApp.AcquireTokenOnBehalfOf(scopes, Assertion).ExecuteAsync().ConfigureAwait(false).GetAwaiter().GetResult().AccessToken;
            // LoggingClient.TrackTrace($"Get Token {newAccessToken} for scopes: {string.Join(' ', scopes)}", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Information);
            return newAccessToken;
        }
        ~AuthDelegateUserImplementation()
        {
            LoggingClient.TrackTrace("~AuthDelegateUserImplementation finalized (GC collected)", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Verbose);
        }
    }
}
