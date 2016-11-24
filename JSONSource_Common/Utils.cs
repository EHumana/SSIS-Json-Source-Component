﻿using Microsoft.SqlServer.Dts.Runtime.Wrapper;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Net.Http;
using System.Text;
#if LINQ_SUPPORTED
using System.Linq;
#endif

namespace com.webkingsoft.JSONSource_Common
{
    public class Utils
    {
        public static object GetVariable(IDTSVariableDispenser100 vd, string varname, out DataType vartype)
        {
            object o = null;
            IDTSVariables100 vars = null;
            try
            {
                vd.LockOneForRead(varname, ref vars);
                o = vars[varname].Value;
                vartype = (DataType)vars[varname].DataType;
                return o;
            }
            finally
            {
                if (vars != null)
                    vars.Unlock();
            }
        }

        private static string DownloadJsonFile(Uri url, string method, IEnumerable<KeyValuePair<string, string>> encodedpars, Dictionary<string, string> headers, ref CookieContainer cookiecontainer, string customLocalTempDir)
        {
            string localTmp = null;
            string filePath = null;



            if (url == null || string.IsNullOrEmpty(url.AbsolutePath))
                throw new ArgumentException("Url parameter was null or empty.");
            if (method == null)
                throw new ArgumentException("Method cannot be null.");

            method = method.ToUpper();
            if (method != "GET" && method != "POST" && method != "PUT" && method != "DELETE")
                throw new ArgumentException("Invalid http method supplied: " + method);

            if (!string.IsNullOrEmpty(customLocalTempDir))
            {
                if (!Directory.Exists(customLocalTempDir))
                    throw new ArgumentException("Local tmp path doesn't exist: " + customLocalTempDir);
                localTmp = customLocalTempDir;
            }
            else
            {
                localTmp = Path.GetTempPath();
            }

            filePath = Path.Combine(localTmp, Guid.NewGuid().ToString() + ".json");

            using (var handler = new HttpClientHandler() { CookieContainer = cookiecontainer })
            using (var client = new HttpClient(handler) { BaseAddress = url })
            {
                // Setto i parametri e gli headers
                handler.AllowAutoRedirect = true;
                handler.UseCookies = true;

                HttpRequestMessage req = new HttpRequestMessage();
                req.RequestUri = url;

                if (headers != null)
                    foreach (var h in headers) {
                        req.Headers.Add(h.Key, h.Value);
                    }

                System.Threading.Tasks.Task<HttpResponseMessage> mtd = null;
                switch (method)
                {
                    case "GET":
                        req.Method = HttpMethod.Get;
                        break;
                    case "POST":
                        req.Method = HttpMethod.Post;
                        var data = new FormUrlEncodedContent(encodedpars);
                        req.Content = data;
                        break;
                    case "PUT":
                        req.Method = HttpMethod.Put;
                        data = new FormUrlEncodedContent(encodedpars);
                        req.Content = data;
                        break;
                    case "DELETE":
                        req.Method = HttpMethod.Delete;
                        break;
                }

                mtd = client.SendAsync(req);

                // Will block
                var result = mtd.Result;

                if (!result.IsSuccessStatusCode)
                {
                    throw new Exception("Status code received was " + result.StatusCode + ", i.e. " + result.ReasonPhrase);
                }

                // Write to file.
                using (var sw = new FileStream(filePath, FileMode.OpenOrCreate))
                    result.Content.CopyToAsync(sw).Wait();
            }
            return filePath;
        }

        /*private static string DownloadJson(Microsoft.SqlServer.Dts.Runtime.VariableDispenser vd, Uri url, string method, IEnumerable<HTTPParameter> pars, string cookievar)
        {
            // Recupera la variabie di cookiecontainer, se specificata
            if (String.IsNullOrEmpty(method))
                throw new ArgumentException("Invalid method specified.");

            method = method.ToUpper();
            UriBuilder b = new UriBuilder(url);

            List<KeyValuePair<string, string>> postParams = new List<KeyValuePair<string, string>>();

            // Componi l'url se sono stati specificati parametri
            if ((method == "GET" || method == "DELETE") && pars != null)
            {

                StringBuilder queryToAppend = new StringBuilder();

                // Per ogni parametro, recupera il valore e codificalo se richiesto.
                foreach (var param in pars)
                {
                    // Name
                    if (param.Encode)
                        queryToAppend.Append(Uri.EscapeUriString(param.Name));
                    else
                        queryToAppend.Append(param.Name);
                    // =
                    queryToAppend.Append("=");
                    // Value
                    string val = null;
                    if (param.Binding == HTTPParamBinding.CustomValue)
                        val = param.Value;
                    else if (param.Binding == HTTPParamBinding.Variable)
                    {
                        DataType type;
                        val = GetVariable(vd, param.Value, out type).ToString();
                    }

                    if (param.Encode)
                        val = Uri.EscapeUriString(val);

                    queryToAppend.Append(val);

                    // Next
                    queryToAppend.Append("&");
                }

                // L'ultimo carattere va scartatao
                if (pars.Count() > 0)
                    queryToAppend.Remove(queryToAppend.Length - 1, 1);


                if (b.Query != null && b.Query.Length > 1)
                    b.Query = b.Query.Substring(1) + "&" + queryToAppend.ToString();
                else
                    b.Query = queryToAppend.ToString();
            }
            else if (pars != null)
            {
                // Costruisci la lista key-value
                foreach (var param in pars)
                {
                    // Name
                    string key = null;
                    string value = null;

                    if (param.Encode)
                        key = Uri.EscapeUriString(param.Name);
                    else
                        key = param.Name;

                    // Value
                    if (param.Binding == HTTPParamBinding.CustomValue)
                        value = param.Value;
                    else if (param.Binding == HTTPParamBinding.Variable)
                    {
                        DataType type;
                        value = GetVariable(vd, param.Value, out type).ToString();
                    }

                    if (param.Encode)
                        value = Uri.EscapeUriString(value);
                    KeyValuePair<string, string> pair = new KeyValuePair<string, string>(key, value);
                    postParams.Add(pair);
                }
            }

            CookieContainer cc = null;
            if (!String.IsNullOrEmpty(cookievar))
            {
                DataType type;
                cc = GetVariable(vd, cookievar, out type) as CookieContainer;
            }
            if (cc == null)
                cc = new CookieContainer();

            string res = DownloadJsonFile(b.Uri, method, postParams, ref cc, null);

            // If the cookie container parameter was not null, assign the container to the variable
            if (!String.IsNullOrEmpty(cookievar))
            {
                Microsoft.SqlServer.Dts.Runtime.Variables vars = null;
                try
                {
                    vd.LockOneForWrite(cookievar, ref vars);
                    vars[cookievar].Value = cc;
                }
                finally
                {
                    if (vars != null)
                        vars.Unlock();
                }

            }

            return res;
        }*/

        public static string DownloadJson(IDTSVariableDispenser100 vd, Uri url, string method, IEnumerable<HTTPParameter> parameters, IEnumerable<HTTPParameter> headers, string cookievar, string customLocalTempDir = null)
        {
            // Recupera la variabie di cookiecontainer, se specificata
            if (String.IsNullOrEmpty(method))
                throw new ArgumentException("Invalid method specified.");

            method = method.ToUpper();
            UriBuilder b = new UriBuilder(url);

            List<KeyValuePair<string,string>> postParams = new List<KeyValuePair<string,string>>();

            // Build the Request URL. 
            // GET and DELETE require parameters to be encoded directly into the URL.
            if ((method == "GET" || method == "DELETE") && parameters != null) {

                StringBuilder queryToAppend = new StringBuilder();
                foreach (var param in parameters) {
                    // Name
                    if (param.Encode)
                        queryToAppend.Append(Uri.EscapeUriString(param.Name));
                    else
                        queryToAppend.Append(param.Name);
                    // =
                    queryToAppend.Append("=");
                    
                    // Value
                    string val = null;
                    if (param.Binding == HTTPParamBinding.CustomValue)
                        val = param.Value;
                    else if (param.Binding == HTTPParamBinding.Variable)
                    {
                        DataType type;
                        val = GetVariable(vd, param.Value, out type).ToString();
                    }
                    else if (param.Binding == HTTPParamBinding.InputField)
                    {
                        val = param.Value;
                    }
                    else {
                        throw new Exception("Unespected binding type");
                    }

                    if (param.Encode)
                        val = Uri.EscapeUriString(val);

                    queryToAppend.Append(val);

                    // Next
                    queryToAppend.Append("&");
                }

                // Discard last character
                if (queryToAppend.Length>0)
                    queryToAppend.Remove(queryToAppend.Length - 1, 1);

                // Handle the case in which query params are mixed: some typed directly into the box and others given by the ad-hoc GUI.
                if (b.Query != null && b.Query.Length > 1) {
                    b.Query = b.Query.Substring(1);
                    if (!String.IsNullOrEmpty(queryToAppend.ToString()))
                        b.Query += "&" + queryToAppend.ToString();
                }
                else
                    b.Query = queryToAppend.ToString();

            }

            // POST and PUT require different handling. Data should be parametrized into the body
            else if (parameters != null) {
                // Costruisci la lista key-value
                foreach (var param in parameters) {
                    // Name
                    string key = null;
                    string value = null;

                    if (param.Encode)
                        key = Uri.EscapeUriString(param.Name);
                    else
                        key = param.Name;
                    
                    // Value
                    if (param.Binding == HTTPParamBinding.CustomValue)
                        value = param.Value;
                    else if (param.Binding == HTTPParamBinding.Variable)
                    {
                        DataType type;
                        value = GetVariable(vd, param.Value, out type).ToString();
                    }

                    if (param.Encode)
                        value = Uri.EscapeUriString(value);
                    KeyValuePair<string,string> pair = new KeyValuePair<string,string>(key,value);
                    postParams.Add(pair);
                }
            }

            Dictionary<string, string> http_hreaders = null;
            // Headers handling
            if (headers != null)
            {
                http_hreaders = new Dictionary<string, string>();
                foreach (var header in headers)
                {
                    // Value
                    string val = null;
                    if (header.Binding == HTTPParamBinding.CustomValue)
                        val = header.Value;
                    else if (header.Binding == HTTPParamBinding.Variable)
                    {
                        DataType type;
                        val = GetVariable(vd, header.Value, out type).ToString();
                    }
                    else if (header.Binding == HTTPParamBinding.InputField)
                    {
                        val = header.Value;
                    }
                    else
                    {
                        throw new Exception("Unespected binding type");
                    }

                    if (header.Encode)
                        val = Uri.EscapeUriString(val);

                    http_hreaders.Add(header.Name, val);
                }
            }

            CookieContainer cc = null;
            if (!String.IsNullOrEmpty(cookievar)) { 
                DataType type;
                cc = GetVariable(vd, cookievar, out type) as CookieContainer;
            }
            if (cc == null)
                cc = new CookieContainer();

            string res = DownloadJsonFile(b.Uri, method, postParams, http_hreaders, ref cc, customLocalTempDir);

            // If the cookie container parameter was not null, assign the container to the variable
            if (!String.IsNullOrEmpty(cookievar))
            {
                IDTSVariables100 vars = null;
                try
                {
                    vd.LockOneForWrite(cookievar, ref vars);
                    vars[cookievar].Value = cc;
                }
                finally {
                    if (vars != null)
                        vars.Unlock();
                }
                
            }

            return res;
            
        }
                
        private static object GetVariable(Microsoft.SqlServer.Dts.Runtime.VariableDispenser vd, string varname, out DataType vartype)
        {
            object o = null;
            Microsoft.SqlServer.Dts.Runtime.Variables vars = null;
            try
            {
                vd.LockOneForRead(varname, ref vars);
                o = vars[varname].Value;
                vartype = (DataType)vars[varname].DataType;
                return o;
            }
            finally
            {
                if (vars != null)
                    vars.Unlock();
            }
        }
    }
}
