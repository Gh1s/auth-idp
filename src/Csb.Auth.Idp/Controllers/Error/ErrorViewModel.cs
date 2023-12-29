using Microsoft.AspNetCore.Mvc;
using System;

namespace Csb.Auth.Idp.Controllers.Error
{
    public class ErrorViewModel
    {
        [FromQuery(Name = "trace_identifier")]
        public string TraceIdentifier { get; set; }

        public Exception Exception { get; set; }

        public string ExceptionPath { get; set; }

        [FromQuery(Name = "error")]
        public string Error { get; set; }

        [FromQuery(Name = "error_debug")]
        public string ErrorDebug { get; set; }

        [FromQuery(Name = "error_description")]
        public string ErrorDescription { get; set; }

        [FromQuery(Name = "error_hint")]
        public string ErrorHint { get; set; }
    }
}
