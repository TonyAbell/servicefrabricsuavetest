namespace OwinHost
open System.Collections.Generic

open Owin
open Microsoft.Owin
open System
open System.Net.Http
open System.Web
open System.Web.Http
open System.Web.Http.Owin
open Microsoft.ServiceFabric.Services.Communication.Runtime
open System.Collections.Generic
open Microsoft.ServiceFabric.Services.Runtime
open System.Fabric
open System.Threading.Tasks
open Microsoft.Owin.Hosting
open Newtonsoft.Json
open System.Net.Http.Formatting
[<RoutePrefix("api")>]
type DefaultController() as self = 
     inherit ApiController()

     [<Route("hello")>]
     member x.Get() = 
        OwinHost.ServiceEventSource.Current.Message("Hi Called")
        x.Ok("hi")




[<Sealed>]
type Startup() =
    
     member __.Configuration(builder: IAppBuilder) =
        let config = new HttpConfiguration()
        config.Formatters.JsonFormatter.SerializerSettings.Formatting <- Formatting.Indented
        config.MapHttpAttributeRoutes()
        builder.UseWebApi(config) |> ignore

type WebApiService () as self =
     inherit StatelessService()

  
     override __.CreateServiceInstanceListeners() : IEnumerable<ServiceInstanceListener> = 
        seq { 
           let factory (p : StatelessServiceInitializationParameters) : ICommunicationListener = 
                OwinHost.ServiceEventSource.Current.ServiceMessage(self, "Owin-Factory Called")
                let mutable serverHandle : IDisposable = Unchecked.defaultof<IDisposable>
                let port = p.CodePackageActivationContext.GetEndpoint("ServiceEndpoint").Port
                //let serviceEndpoint = p.CodePackageActivationContext.GetEndpoint("ServiceEndpoint");
                let startup = new Startup()
                let stopServer() =
                    if not <| isNull serverHandle  then
                        OwinHost.ServiceEventSource.Current.ServiceMessage(self, "Owin-Stoping Server")
                        serverHandle.Dispose()
                { new ICommunicationListener with
                      member __.Abort() = 
                        stopServer()
                        ()
                      member __.CloseAsync _ = 
                        OwinHost.ServiceEventSource.Current.ServiceMessage(self, "Owin-Close Async Called")
                        stopServer()
                        Task.FromResult() :> Task
                      member __.OpenAsync cancellationToken = 
                          OwinHost.ServiceEventSource.Current.ServiceMessage(self, "Owin-Open Async Called")
                          let listeningAddress  = sprintf "http://+:%d" port
                          let publishAddress = listeningAddress.Replace("+",FabricRuntime.GetNodeContext().IPAddressOrFQDN)
                          serverHandle <- WebApp.Start(listeningAddress,fun appBuilder -> startup.Configuration(appBuilder) )
                          OwinHost.ServiceEventSource.Current.ServiceMessage(self, "Owin-Listening Address-{0}",listeningAddress)
                          OwinHost.ServiceEventSource.Current.ServiceMessage(self, "Owin-Publish Address-{0}",publishAddress)
                          Task.FromResult(publishAddress)
                        }
           yield new ServiceInstanceListener(new System.Func<StatelessServiceInitializationParameters, ICommunicationListener>(factory))
        }