using Csb.Auth.Idp.Controllers.Error;
using FluentAssertions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using Xunit;

namespace Csb.Auth.Idp.Tests.Controllers.Error
{
    public class ErrorControllerTests
    {
        [Fact]
        public void Error()
        {
            // Setup
            var exception = new InvalidOperationException();
            var exceptionPath = "/path";
            var exceptionHandlerPathFeatureMock = new Mock<IExceptionHandlerPathFeature>();
            exceptionHandlerPathFeatureMock.SetupGet(m => m.Error).Returns(exception);
            exceptionHandlerPathFeatureMock.SetupGet(m => m.Path).Returns(exceptionPath);
            var featureCollectionMock = new Mock<IFeatureCollection>();
            featureCollectionMock.Setup(m => m.Get<IExceptionHandlerPathFeature>()).Returns(exceptionHandlerPathFeatureMock.Object);
            var model = new ErrorViewModel
            {
                Error = "error",
                ErrorDescription = "error_description",
                ErrorHint = "error_hint",
                ErrorDebug = "error_debug",
                TraceIdentifier = Guid.NewGuid().ToString()
            };
            var subject = new ErrorController
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext(featureCollectionMock.Object)
                }
            };

            // Act
            var result = subject.Error(model);

            // Assert
            result.Should().BeOfType<ViewResult>().Which.Model.Should().Be(model);
            model.Exception.Should().Be(exception);
            model.ExceptionPath.Should().Be(exceptionPath);
        }
    }
}
