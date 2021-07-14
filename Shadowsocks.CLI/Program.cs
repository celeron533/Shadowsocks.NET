using Splat;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Shadowsocks.CLI
{
    internal class Program
    {
        private static Task<int> Main(string[] args)
        {
            Locator.CurrentMutable.RegisterConstant<ILogger>(new ConsoleLogger());

            var backendOption = new Option<Backend>("--backend", "Shadowsocks backend to use. Available backends: shadowsocks-rust, v2ray, legacy, pipelines.");
            var listenOption = new Option<IPEndPoint?>("--listen", Parsers.ParseIPEndPoint, false, "Address and port to listen on for both SOCKS5 and HTTP proxy.");
            var listenSocksOption = new Option<IPEndPoint?>("--listen-socks", Parsers.ParseIPEndPoint, false, "Address and port to listen on for SOCKS5 proxy.");
            var listenHttpOption = new Option<IPEndPoint?>("--listen-http", Parsers.ParseIPEndPoint, false, "Address and port to listen on for HTTP proxy.");
            var serverAddressOption = new Option<string>("--server-address", "Address of the remote Shadowsocks server to connect to.")
            {
                Arity = ArgumentArity.ExactlyOne,
            };
            var serverPortOption = new Option<int>("--server-port", Parsers.ParsePortNumber, false, "Port of the remote Shadowsocks server to connect to.")
            {
                Arity = ArgumentArity.ExactlyOne,
            };
            var methodOption = new Option<string>("--method", "Encryption method to use for remote Shadowsocks server.")
            {
                Arity = ArgumentArity.ExactlyOne,
            };
            var passwordOption = new Option<string>("--password", "Password to use for remote Shadowsocks server.");
            var keyOption = new Option<string>("--key", "Encryption key (NOT password!) to use for remote Shadowsocks server.");
            var pluginPathOption = new Option<string>("--plugin-path", "Plugin binary path.");
            var pluginOptsOption = new Option<string>("--plugin-opts", "Plugin options.");
            var pluginArgsOption = new Option<string>("--plugin-args", "Plugin startup arguments.");

            var clientCommand = new Command("client", "Shadowsocks client.");
            clientCommand.AddAlias("c");
            clientCommand.AddOption(backendOption);
            clientCommand.AddOption(listenOption);
            clientCommand.AddOption(listenSocksOption);
            clientCommand.AddOption(listenHttpOption);
            clientCommand.AddOption(serverAddressOption);
            clientCommand.AddOption(serverPortOption);
            clientCommand.AddOption(methodOption);
            clientCommand.AddOption(passwordOption);
            clientCommand.AddOption(keyOption);
            clientCommand.AddOption(pluginPathOption);
            clientCommand.AddOption(pluginOptsOption);
            clientCommand.AddOption(pluginArgsOption);
            clientCommand.AddValidator(ClientCommand.ValidateClientCommand);
            clientCommand.Handler = CommandHandler.Create<Backend, IPEndPoint?, IPEndPoint?, IPEndPoint?, string, int, string, string, string, string, string, string, CancellationToken>(ClientCommand.RunClientAsync);

            var serverCommand = new Command("server", "Shadowsocks server.");
            serverCommand.AddAlias("s");
            serverCommand.Handler = CommandHandler.Create(() => LogHost.Default.Error("Not implemented."));

            var convertConfigCommand = new Command("convert-config", "Convert between different config formats. Supported formats: SIP002 links, SIP008 delivery JSON, and V2Ray JSON (outbound only).");
            convertConfigCommand.AddOption(new Option<string[]?>("--from-urls", "URL conversion sources. Multiple URLs are supported. Supported protocols are ss:// and https://."));
            convertConfigCommand.AddOption(new Option<string[]?>("--from-oocv1-api-tokens", "Open Online Config 1 API token sources. Multiple tokens are supported."));
            convertConfigCommand.AddOption(new Option<string[]?>("--from-oocv1-json", "Open Online Config 1 JSON conversion sources. Multiple JSON files are supported."));
            convertConfigCommand.AddOption(new Option<string[]?>("--from-sip008-json", "SIP008 JSON conversion sources. Multiple JSON files are supported."));
            convertConfigCommand.AddOption(new Option<string[]?>("--from-v2ray-json", "V2Ray JSON conversion sources. Multiple JSON files are supported."));
            convertConfigCommand.AddOption(new Option<bool>("--prefix-group-name", "Whether to prefix group name to server names after conversion."));
            convertConfigCommand.AddOption(new Option<bool>("--to-urls", "Convert to ss:// links and print."));
            convertConfigCommand.AddOption(new Option<string>("--to-oocv1-json", "Convert to Open Online Config 1 JSON and save to the specified path."));
            convertConfigCommand.AddOption(new Option<string>("--to-sip008-json", "Convert to SIP008 JSON and save to the specified path."));
            convertConfigCommand.AddOption(new Option<string>("--to-v2ray-json", "Convert to V2Ray JSON and save to the specified path."));
            convertConfigCommand.Handler = CommandHandler.Create(
                async (string[]? fromUrls, string[]? fromOocv1ApiTokens, string[]? fromOocv1Json, string[]? fromSip008Json, string[]? fromV2rayJson, bool prefixGroupName, bool toUrls, string toOocv1Json, string toSip008Json, string toV2rayJson, CancellationToken cancellationToken) =>
                {
                    var configConverter = new ConfigConverter(prefixGroupName);

                    try
                    {
                        if (fromUrls != null)
                        {
                            var uris = new List<Uri>();
                            foreach (var url in fromUrls)
                            {
                                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                                    uris.Add(uri);
                                else
                                    Console.WriteLine($"Invalid URL: {url}");
                            }
                            await configConverter.FromUrls(uris, cancellationToken);
                        }
                        if (fromOocv1ApiTokens is not null)
                            await configConverter.FromOOCv1ApiTokens(fromOocv1ApiTokens, cancellationToken);
                        if (fromOocv1Json is not null)
                            await configConverter.FromOOCv1Json(fromOocv1Json, cancellationToken);
                        if (fromSip008Json != null)
                            await configConverter.FromSip008Json(fromSip008Json, cancellationToken);
                        if (fromV2rayJson != null)
                            await configConverter.FromV2rayJson(fromV2rayJson, cancellationToken);

                        if (toUrls)
                        {
                            var uris = configConverter.ToUrls();
                            foreach (var uri in uris)
                                Console.WriteLine(uri.AbsoluteUri);
                        }
                        if (!string.IsNullOrEmpty(toOocv1Json))
                            await configConverter.ToOOCv1Json(toOocv1Json, cancellationToken);
                        if (!string.IsNullOrEmpty(toSip008Json))
                            await configConverter.ToSip008Json(toSip008Json, cancellationToken);
                        if (!string.IsNullOrEmpty(toV2rayJson))
                            await configConverter.ToV2rayJson(toV2rayJson, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                });

            var utilitiesCommand = new Command("utilities", "Shadowsocks-related utilities.")
            {
                convertConfigCommand,
            };
            utilitiesCommand.AddAlias("u");
            utilitiesCommand.AddAlias("util");
            utilitiesCommand.AddAlias("utils");

            var rootCommand = new RootCommand("CLI for Shadowsocks server and client implementation in C#.")
            {
                clientCommand,
                serverCommand,
                utilitiesCommand,
            };

            Console.OutputEncoding = Encoding.UTF8;
            return rootCommand.InvokeAsync(args);
        }
    }
}
