using FluentAssertions;
using Microsoft.AspNetCore.WebUtilities;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Csb.Auth.Idp.FunctionalTests
{
    public class AuthorizationCodeFlowTests : IClassFixture<FunctionalTestsFixture<Samples.AuthorizationCodeMvc.Startup>>
    {
        private const int RedirectTimeoutSeconds = 15;
        private const string IndexPageUrl = "https://localhost:5900/";

        private readonly FunctionalTestsFixture<Samples.AuthorizationCodeMvc.Startup> _fixture;

        public AuthorizationCodeFlowTests(FunctionalTestsFixture<Samples.AuthorizationCodeMvc.Startup> fixture)
        {
            _fixture = fixture;
        }

        [Theory]
        [InlineData(FunctionalTestsConstants.WebDrivers.Chrome)]
        [InlineData(FunctionalTestsConstants.WebDrivers.Edge)]
        [InlineData(FunctionalTestsConstants.WebDrivers.Firefox)]
        public void Login_ValidCredentials(string driverName)
        {
            using (var driver = _fixture.CreateWebDriver(driverName))
            {
                var waiter = new WebDriverWait(driver, TimeSpan.FromSeconds(RedirectTimeoutSeconds));

                // Navigating to the index page should redirect us to the IDP login page.
                driver.Navigate().GoToUrl(IndexPageUrl);
                waiter.Until(driver => driver.Url.StartsWith(FunctionalTestsConstants.UI.LoginPageUrl));

                // Checking the URL if it matches the login page URL.
                var loginPageUrl = new Uri(driver.Url);
                loginPageUrl.AbsoluteUri.Should().StartWith(FunctionalTestsConstants.UI.LoginPageUrl);
                var loginPageQueryString = QueryHelpers.ParseQuery(loginPageUrl.Query);
                loginPageQueryString.Should().ContainKey("login_challenge").WhichValue.Should().HaveCountGreaterThan(0);

                // Submitting the form.
                driver.FindElement(By.Id("Username")).SendKeys(FunctionalTestsConstants.TestUser.Username);
                driver.FindElement(By.Id("Password")).SendKeys(FunctionalTestsConstants.TestUser.Password);
                driver.FindElement(By.CssSelector("[type=submit]")).Click();
                waiter.Until(driver => driver.Url.StartsWith(IndexPageUrl));

                // Checking the claims displayed on the page.
                var claims = driver
                    .FindElement(By.Id("claims"))
                    .FindElements(By.TagName("li"))
                    .Select(item =>
                    {
                        var claim = item.Text.Split('=', StringSplitOptions.RemoveEmptyEntries);
                        return new KeyValuePair<string, string>(claim[0], claim[1].TrimStart());
                    })
                    .ToDictionary(item => item.Key, item => item.Value);
                claims.Should().Contain(FunctionalTestsConstants.TestUser.Claims);
            }
        }

        [Theory]
        [InlineData(FunctionalTestsConstants.WebDrivers.Chrome)]
        [InlineData(FunctionalTestsConstants.WebDrivers.Edge)]
        [InlineData(FunctionalTestsConstants.WebDrivers.Firefox)]
        public void Login_InvalidCredentials(string driverName)
        {
            using (var driver = _fixture.CreateWebDriver(driverName))
            {
                var waiter = new WebDriverWait(driver, TimeSpan.FromSeconds(RedirectTimeoutSeconds));

                // Navigating to the index page should redirect us to the IDP login page.
                driver.Navigate().GoToUrl(IndexPageUrl);
                waiter.Until(driver => driver.Url.StartsWith(FunctionalTestsConstants.UI.LoginPageUrl));

                // Checking the URL if it matches the login page URL.
                var loginPageUrl = new Uri(driver.Url);
                loginPageUrl.AbsoluteUri.Should().StartWith(FunctionalTestsConstants.UI.LoginPageUrl);
                var loginPageQueryString = QueryHelpers.ParseQuery(loginPageUrl.Query);
                loginPageQueryString.Should().ContainKey("login_challenge").WhichValue.Should().HaveCountGreaterThan(0);

                // Submitting the form.
                driver.FindElement(By.Id("Username")).SendKeys("invalid_user");
                driver.FindElement(By.Id("Password")).SendKeys("invalid_password");
                driver.FindElement(By.CssSelector("[type=submit]")).Click();

                // Checking if the URL is sill the login page URL.
                driver.Url.Should().StartWith(FunctionalTestsConstants.UI.LoginPageUrl);
                driver
                    .FindElement(By.ClassName("validation-summary-errors"))
                    .FindElement(By.TagName("ul"))
                    .FindElements(By.TagName("li"))
                    .Should()
                    .HaveCount(1)
                    .And
                    .Match(items => items.Any(t => t.Text == "An error has occured while interacting with the underlying user store"));
            }
        }

        [Theory]
        [InlineData(FunctionalTestsConstants.WebDrivers.Chrome)]
        [InlineData(FunctionalTestsConstants.WebDrivers.Edge)]
        [InlineData(FunctionalTestsConstants.WebDrivers.Firefox)]
        public void Logout_NoPrompt(string driverName)
        {
            using (var driver = _fixture.CreateWebDriver(driverName))
            {
                var waiter = new WebDriverWait(driver, TimeSpan.FromSeconds(RedirectTimeoutSeconds));

                // Navigating to the index page should redirect us to the IDP login page.
                driver.Navigate().GoToUrl(IndexPageUrl);
                waiter.Until(driver => driver.Url.StartsWith(FunctionalTestsConstants.UI.LoginPageUrl));

                // Checking the URL if it matches the login page URL.
                var loginPageUrl = new Uri(driver.Url);
                loginPageUrl.AbsoluteUri.Should().StartWith(FunctionalTestsConstants.UI.LoginPageUrl);
                var loginPageQueryString = QueryHelpers.ParseQuery(loginPageUrl.Query);
                loginPageQueryString.Should().ContainKey("login_challenge").WhichValue.Should().HaveCountGreaterThan(0);

                // Submitting the form.
                driver.FindElement(By.Id("Username")).SendKeys(FunctionalTestsConstants.TestUser.Username);
                driver.FindElement(By.Id("Password")).SendKeys(FunctionalTestsConstants.TestUser.Password);
                driver.FindElement(By.CssSelector("[type=submit]")).Click();
                waiter.Until(driver => driver.Url.StartsWith(IndexPageUrl));

                // Clicking the logout button.
                driver.FindElement(By.Id("logout")).Click();
                waiter.Until(driver => driver.Url.StartsWith(FunctionalTestsConstants.UI.LoginPageUrl));

                // Checking the URL if has correctly redirected the user to the login page.
                driver.Url.Should().StartWith(FunctionalTestsConstants.UI.LoginPageUrl);
            }
        }

        [Theory]
        [InlineData(FunctionalTestsConstants.WebDrivers.Chrome)]
        [InlineData(FunctionalTestsConstants.WebDrivers.Edge)]
        [InlineData(FunctionalTestsConstants.WebDrivers.Firefox)]
        public void Logout_Prompt_Yes(string driverName)
        {
            using (var driver = _fixture.CreateWebDriver(driverName))
            {
                var waiter = new WebDriverWait(driver, TimeSpan.FromSeconds(RedirectTimeoutSeconds));

                // Navigating to the index page should redirect us to the IDP login page.
                driver.Navigate().GoToUrl(IndexPageUrl);
                waiter.Until(driver => driver.Url.StartsWith(FunctionalTestsConstants.UI.LoginPageUrl));

                // Checking the URL if it matches the login page URL.
                var loginPageUrl = new Uri(driver.Url);
                loginPageUrl.AbsoluteUri.Should().StartWith(FunctionalTestsConstants.UI.LoginPageUrl);
                var loginPageQueryString = QueryHelpers.ParseQuery(loginPageUrl.Query);
                loginPageQueryString.Should().ContainKey("login_challenge").WhichValue.Should().HaveCountGreaterThan(0);

                // Submitting the form.
                driver.FindElement(By.Id("Username")).SendKeys(FunctionalTestsConstants.TestUser.Username);
                driver.FindElement(By.Id("Password")).SendKeys(FunctionalTestsConstants.TestUser.Password);
                driver.FindElement(By.CssSelector("#RememberMe + label")).Click();
                driver.FindElement(By.CssSelector("[type=submit]")).Click();
                waiter.Until(driver => driver.Url.StartsWith(IndexPageUrl));

                // Clicking the logout button.
                driver.FindElement(By.Id("logout")).Click();
                waiter.Until(driver => driver.Url.StartsWith(FunctionalTestsConstants.UI.LogoutPageUrl));

                // Clicking the "Yes" button.
                driver.FindElement(By.Id("accept-form")).Submit();
                waiter.Until(driver => driver.Url.StartsWith(FunctionalTestsConstants.UI.LoginPageUrl));

                // Checking the URL if has correctly redirected the user to the login page.
                driver.Url.Should().StartWith(FunctionalTestsConstants.UI.LoginPageUrl);
            }
        }

        [Theory]
        [InlineData(FunctionalTestsConstants.WebDrivers.Chrome)]
        [InlineData(FunctionalTestsConstants.WebDrivers.Edge)]
        [InlineData(FunctionalTestsConstants.WebDrivers.Firefox)]
        public void Logout_Prompt_No(string driverName)
        {
            using (var driver = _fixture.CreateWebDriver(driverName))
            {
                var waiter = new WebDriverWait(driver, TimeSpan.FromSeconds(RedirectTimeoutSeconds));

                // Navigating to the index page should redirect us to the IDP login page.
                driver.Navigate().GoToUrl(IndexPageUrl);
                waiter.Until(driver => driver.Url.StartsWith(FunctionalTestsConstants.UI.LoginPageUrl));

                // Checking the URL if it matches the login page URL.
                var loginPageUrl = new Uri(driver.Url);
                loginPageUrl.AbsoluteUri.Should().StartWith(FunctionalTestsConstants.UI.LoginPageUrl);
                var loginPageQueryString = QueryHelpers.ParseQuery(loginPageUrl.Query);
                loginPageQueryString.Should().ContainKey("login_challenge").WhichValue.Should().HaveCountGreaterThan(0);

                // Submitting the form.
                driver.FindElement(By.Id("Username")).SendKeys(FunctionalTestsConstants.TestUser.Username);
                driver.FindElement(By.Id("Password")).SendKeys(FunctionalTestsConstants.TestUser.Password);
                driver.FindElement(By.CssSelector("#RememberMe + label")).Click();
                driver.FindElement(By.CssSelector("[type=submit]")).Click();
                waiter.Until(driver => driver.Url.StartsWith(IndexPageUrl));

                // Clicking the logout button.
                driver.FindElement(By.Id("logout")).Click();
                waiter.Until(driver => driver.Url.StartsWith(FunctionalTestsConstants.UI.LogoutPageUrl));

                // Clicking the "No" button.
                driver.FindElement(By.Id("deny-form")).Submit();
                waiter.Until(driver => driver.Url.StartsWith(IndexPageUrl));

                // Checking the URL if has correctly redirected the user to the login page.
                driver.Url.Should().StartWith(IndexPageUrl);
            }
        }

        [Theory]
        [InlineData(FunctionalTestsConstants.WebDrivers.Chrome)]
        [InlineData(FunctionalTestsConstants.WebDrivers.Edge)]
        [InlineData(FunctionalTestsConstants.WebDrivers.Firefox)]
        public void Logout_Global_Prompt_Yes(string driverName)
        {
            using (var driver = _fixture.CreateWebDriver(driverName))
            {
                var waiter = new WebDriverWait(driver, TimeSpan.FromSeconds(RedirectTimeoutSeconds));

                // Navigating to the index page should redirect us to the IDP login page.
                driver.Navigate().GoToUrl(IndexPageUrl);
                waiter.Until(driver => driver.Url.StartsWith(FunctionalTestsConstants.UI.LoginPageUrl));

                // Checking the URL if it matches the login page URL.
                var loginPageUrl = new Uri(driver.Url);
                loginPageUrl.AbsoluteUri.Should().StartWith(FunctionalTestsConstants.UI.LoginPageUrl);
                var loginPageQueryString = QueryHelpers.ParseQuery(loginPageUrl.Query);
                loginPageQueryString.Should().ContainKey("login_challenge").WhichValue.Should().HaveCountGreaterThan(0);

                // Submitting the form.
                driver.FindElement(By.Id("Username")).SendKeys(FunctionalTestsConstants.TestUser.Username);
                driver.FindElement(By.Id("Password")).SendKeys(FunctionalTestsConstants.TestUser.Password);
                driver.FindElement(By.CssSelector("#RememberMe + label")).Click();
                driver.FindElement(By.CssSelector("[type=submit]")).Click();
                waiter.Until(driver => driver.Url.StartsWith(IndexPageUrl));

                // Navigating to the global logout page.
                driver.Navigate().GoToUrl(FunctionalTestsConstants.UI.LogoutPageUrl);
                waiter.Until(driver => driver.Url.StartsWith(FunctionalTestsConstants.UI.LogoutPageUrl));

                // Clicking the "Yes" button.
                driver.FindElement(By.Id("accept-form")).Submit();
                waiter.Until(driver => driver.Url.StartsWith(FunctionalTestsConstants.UI.LoggedOutPageUrl));

                // Checking the URL if has correctly redirected the user to the login page.
                driver.Url.Should().StartWith(FunctionalTestsConstants.UI.LoggedOutPageUrl);
            }
        }

        [Theory]
        [InlineData(FunctionalTestsConstants.WebDrivers.Chrome)]
        [InlineData(FunctionalTestsConstants.WebDrivers.Edge)]
        [InlineData(FunctionalTestsConstants.WebDrivers.Firefox)]
        public void Logout_Global_Prompt_No(string driverName)
        {
            using (var driver = _fixture.CreateWebDriver(driverName))
            {
                var waiter = new WebDriverWait(driver, TimeSpan.FromSeconds(RedirectTimeoutSeconds));

                // Navigating to the index page should redirect us to the IDP login page.
                driver.Navigate().GoToUrl(IndexPageUrl);
                waiter.Until(driver => driver.Url.StartsWith(FunctionalTestsConstants.UI.LoginPageUrl));

                // Checking the URL if it matches the login page URL.
                var loginPageUrl = new Uri(driver.Url);
                loginPageUrl.AbsoluteUri.Should().StartWith(FunctionalTestsConstants.UI.LoginPageUrl);
                var loginPageQueryString = QueryHelpers.ParseQuery(loginPageUrl.Query);
                loginPageQueryString.Should().ContainKey("login_challenge").WhichValue.Should().HaveCountGreaterThan(0);

                // Submitting the form.
                driver.FindElement(By.Id("Username")).SendKeys(FunctionalTestsConstants.TestUser.Username);
                driver.FindElement(By.Id("Password")).SendKeys(FunctionalTestsConstants.TestUser.Password);
                driver.FindElement(By.CssSelector("#RememberMe + label")).Click();
                driver.FindElement(By.CssSelector("[type=submit]")).Click();
                waiter.Until(driver => driver.Url.StartsWith(IndexPageUrl));

                // Clicking the logout button.
                driver.Navigate().GoToUrl(FunctionalTestsConstants.UI.LogoutPageUrl);
                waiter.Until(driver => driver.Url.StartsWith(FunctionalTestsConstants.UI.LogoutPageUrl));

                // Clicking the "No" button.
                driver.FindElement(By.Id("deny-form")).Submit();
                waiter.Until(driver => driver.FindElement(By.CssSelector(".container > p")).Text == "You haven't been disconnected from all applications.");
            }
        }
    }
}
