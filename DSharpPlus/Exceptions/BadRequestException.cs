// This file is part of the DSharpPlus project.
//
// Copyright (c) 2015 Mike Santiago
// Copyright (c) 2016-2022 DSharpPlus Contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using DSharpPlus.Net;
using Newtonsoft.Json.Linq;

namespace DSharpPlus.Exceptions
{
    /// <summary>
    /// Represents an exception thrown when a malformed request is sent.
    /// </summary>
    public class BadRequestException : DiscordException
    {

        /// <summary>
        /// Gets the error code for this exception.
        /// </summary>
        public int Code { get; internal set; }

        /// <summary>
        /// Gets the form error responses in JSON format.
        /// </summary>
        public string Errors { get; internal set; }

        internal BadRequestException(BaseRestRequest request, RestResponse response) : base("Bad request: " + response.ResponseCode)
        {
            WebRequest = request;
            WebResponse = response;

            try
            {
                var j = JObject.Parse(response.Response);

                if (j["code"] != null)
                    Code = (int)j["code"];

                if (j["message"] != null)
                    JsonMessage = j["message"].ToString();

                if (j["errors"] != null)
                    Errors = j["errors"].ToString();
            }
            catch { }
        }
    }
}
