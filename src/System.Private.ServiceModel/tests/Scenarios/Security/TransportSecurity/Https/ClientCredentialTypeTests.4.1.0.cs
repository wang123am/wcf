// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Security;
using System.Text;
using Xunit;
using Infrastructure.Common;

public class Https_ClientCredentialTypeTests : ConditionalWcfTest
{
    private static string s_username;
    private static string s_password;
    private const string BasicUsernameHeaderName = "BasicUsername";
    private const string BasicPasswordHeaderName = "BasicPassword";

    static Https_ClientCredentialTypeTests()
    {
        s_username = TestProperties.GetProperty(TestProperties.ExplicitUserName_PropertyName);
        s_password = TestProperties.GetProperty(TestProperties.ExplicitPassword_PropertyName);
    }


    [WcfFact]
    [Condition(nameof(Root_Certificate_Installed))]
    [OuterLoop]
    public static void BasicAuthentication_RoundTrips_Echo()
    {
        StringBuilder errorBuilder = new StringBuilder();

        try
        {
            BasicHttpBinding basicHttpBinding = new BasicHttpBinding(BasicHttpSecurityMode.Transport);
            basicHttpBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Basic;

            ChannelFactory<IWcfCustomUserNameService> factory = new ChannelFactory<IWcfCustomUserNameService>(basicHttpBinding, new EndpointAddress(Endpoints.Https_BasicAuth_Address));
            string username = Guid.NewGuid().ToString("n").Substring(0, 8);
            string password = Guid.NewGuid().ToString("n").Substring(0, 16);
            factory.Credentials.UserName.UserName = username;
            factory.Credentials.UserName.Password = password;

            IWcfCustomUserNameService serviceProxy = factory.CreateChannel();

            string testString = "I am a test";
            string result;
            using (var scope = new OperationContextScope((IContextChannel)serviceProxy))
            {
                HttpRequestMessageProperty requestMessageProperty;
                if (!OperationContext.Current.OutgoingMessageProperties.ContainsKey(HttpRequestMessageProperty.Name))
                {
                    requestMessageProperty = new HttpRequestMessageProperty();
                    OperationContext.Current.OutgoingMessageProperties[HttpRequestMessageProperty.Name] = requestMessageProperty;
                }
                else
                {
                    requestMessageProperty = (HttpRequestMessageProperty)OperationContext.Current.OutgoingMessageProperties[HttpRequestMessageProperty.Name];
                }

                requestMessageProperty.Headers[BasicUsernameHeaderName] = username;
                requestMessageProperty.Headers[BasicPasswordHeaderName] = password;

                result = serviceProxy.Echo(testString);
            }

            bool success = string.Equals(result, testString);

            if (!success)
            {
                errorBuilder.AppendLine(string.Format("Basic echo test.\nTest variation:...\n{0}\nUsing address: '{1}'", "BasicAuthentication_RoundTrips_Echo", Endpoints.Https_BasicAuth_Address));
                errorBuilder.AppendLine(String.Format("    Error: expected response from service: '{0}' Actual was: '{1}'", testString, result));
            }
        }
        catch (Exception ex)
        {
            errorBuilder.AppendLine(string.Format("Basic echo test.\nTest variation:...\n{0}\nUsing address: '{1}'", "BasicAuthentication_RoundTrips_Echo", Endpoints.Https_BasicAuth_Address));
            errorBuilder.AppendLine(String.Format("Unexpected exception was caught: {0}", ex.ToString()));
        }

        Assert.True(errorBuilder.Length == 0, String.Format("Test Case: BasicAuthentication FAILED with the following errors: {0}", errorBuilder));
    }


    [WcfFact]
    [Condition(nameof(Root_Certificate_Installed))]
    [OuterLoop]
    public static void BasicAuthenticationInvalidPwd_throw_MessageSecurityException()
    {
        StringBuilder errorBuilder = new StringBuilder();
        // Will need to use localized string once it is available
        // On Native retail, the message is stripped to 'HttpAuthorizationForbidden, Basic'
        // On Debug or .Net Core, the entire message is "The HTTP request was forbidden with client authentication scheme 'Basic'."
        // Thus we will only check message contains "forbidden"
        string message = "forbidden";

        MessageSecurityException exception = Assert.Throws<MessageSecurityException>(() =>
        {
            BasicHttpBinding basicHttpBinding = new BasicHttpBinding(BasicHttpSecurityMode.Transport);
            basicHttpBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Basic;

            ChannelFactory<IWcfCustomUserNameService> factory = new ChannelFactory<IWcfCustomUserNameService>(basicHttpBinding, new EndpointAddress(Endpoints.Https_BasicAuth_Address));
            string username = Guid.NewGuid().ToString("n").Substring(0, 8);
            string password = Guid.NewGuid().ToString("n").Substring(0, 16);
            factory.Credentials.UserName.UserName = username;
            factory.Credentials.UserName.Password = password + "Invalid";

            IWcfCustomUserNameService serviceProxy = factory.CreateChannel();

            string testString = "I am a test";
            using (var scope = new OperationContextScope((IContextChannel)serviceProxy))
            {
                HttpRequestMessageProperty requestMessageProperty;
                if (!OperationContext.Current.OutgoingMessageProperties.ContainsKey(HttpRequestMessageProperty.Name))
                {
                    requestMessageProperty = new HttpRequestMessageProperty();
                    OperationContext.Current.OutgoingMessageProperties[HttpRequestMessageProperty.Name] = requestMessageProperty;
                }
                else
                {
                    requestMessageProperty = (HttpRequestMessageProperty)OperationContext.Current.OutgoingMessageProperties[HttpRequestMessageProperty.Name];
                }

                requestMessageProperty.Headers[BasicUsernameHeaderName] = username;
                requestMessageProperty.Headers[BasicPasswordHeaderName] = password;
                string result = serviceProxy.Echo(testString);
            }
        });

        Assert.True(exception.Message.ToLower().Contains(message), string.Format("Expected exception message to contain: '{0}', actual message is: '{1}'", message, exception.Message));
    }

    [WcfFact]
    [Condition(nameof(Root_Certificate_Installed))]
    [OuterLoop]
    public static void BasicAuthenticationEmptyUser_throw_ArgumentException()
    {
        StringBuilder errorBuilder = new StringBuilder();
        string paraMessage = "username";

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
        {
            BasicHttpBinding basicHttpBinding = new BasicHttpBinding(BasicHttpSecurityMode.Transport);
            basicHttpBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Basic;

            ChannelFactory<IWcfCustomUserNameService> factory = new ChannelFactory<IWcfCustomUserNameService>(basicHttpBinding, new EndpointAddress(Endpoints.Https_BasicAuth_Address));
            factory.Credentials.UserName.UserName = "";
            factory.Credentials.UserName.Password = "NoUserName";

            IWcfCustomUserNameService serviceProxy = factory.CreateChannel();

            string testString = "I am a test";
            string result = serviceProxy.Echo(testString);
        });

        Assert.True(exception.Message.ToLower().Contains(paraMessage), string.Format("Expected exception message to contain: '{0}', actual: '{1}'", paraMessage, exception.Message));
    }

    [WcfFact]
    [Condition(nameof(Server_Domain_Joined),
               nameof(Root_Certificate_Installed),
               nameof(Digest_Authentication_Available),
               nameof(Explicit_Credentials_Available))]
    [OuterLoop]
    // Test Requirements \\
    // The following environment variables must be set...
    //          "ExplicitUserName"
    //          "ExplicitPassword"
    public static void DigestAuthentication_RoundTrips_Echo()
    {
        StringBuilder errorBuilder = new StringBuilder();

        try
        {
            BasicHttpBinding basicHttpBinding = new BasicHttpBinding(BasicHttpSecurityMode.Transport);
            basicHttpBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Digest;

            Action<ChannelFactory> credentials = (factory) =>
            {
                factory.Credentials.HttpDigest.ClientCredential.UserName = s_username;
                factory.Credentials.HttpDigest.ClientCredential.Password = s_password;
            };

            ScenarioTestHelpers.RunBasicEchoTest(basicHttpBinding, Endpoints.Https_DigestAuth_Address, "BasicHttpBinding - Digest auth ", errorBuilder, credentials);
        }
        catch (Exception ex)
        {
            errorBuilder.AppendLine(String.Format("Unexpected exception was caught: {0}", ex.ToString()));
        }

        Assert.True(errorBuilder.Length == 0, String.Format("Test Case: DigestAuthentication FAILED with the following errors: {0}", errorBuilder));
    }

    [WcfFact]
    [Condition(nameof(NTLM_Available), nameof(Root_Certificate_Installed))]
    [OuterLoop]
    public static void NtlmAuthentication_RoundTrips_Echo()
    {
        StringBuilder errorBuilder = new StringBuilder();

        try
        {
            BasicHttpBinding basicHttpBinding = new BasicHttpBinding(BasicHttpSecurityMode.Transport);
            basicHttpBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Ntlm;

            ScenarioTestHelpers.RunBasicEchoTest(basicHttpBinding, Endpoints.Https_NtlmAuth_Address, "BasicHttpBinding with NTLM authentication", errorBuilder, null);
        }
        catch (Exception ex)
        {
            errorBuilder.AppendLine(String.Format("Unexpected exception was caught: {0}", ex.ToString()));
        }

        Assert.True(errorBuilder.Length == 0, String.Format("Test Case: NtlmAuthentication FAILED with the following errors: {0}", errorBuilder));
    }

    [WcfFact]
    [Condition(nameof(Windows_Authentication_Available), nameof(Is_Windows))]
    [OuterLoop]
    public static void WindowsAuthentication_RoundTrips_Echo()
    {
        StringBuilder errorBuilder = new StringBuilder();

        try
        {
            BasicHttpBinding basicHttpBinding = new BasicHttpBinding(BasicHttpSecurityMode.TransportCredentialOnly);
            basicHttpBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Windows;

            ScenarioTestHelpers.RunBasicEchoTest(basicHttpBinding, Endpoints.Http_WindowsAuth_Address, "BasicHttpBinding with Windows authentication", errorBuilder, null);
        }
        catch (Exception ex)
        {
            errorBuilder.AppendLine(String.Format("Unexpected exception was caught: {0}", ex.ToString()));
        }

        Assert.True(errorBuilder.Length == 0, String.Format("Test Case: WindowsAuthentication FAILED with the following errors: {0}", errorBuilder));
    }
}
