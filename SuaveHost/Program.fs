// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.
open Microsoft.ServiceFabric.Services
open Suave
open Suave.Http.Successful
open Suave.Types
open Suave.Utils
open Suave.Web
open System
open System.Fabric
open System.Threading
open System.Threading.Tasks
open Suave.Http
open Suave.Http.Applicatives
open Microsoft.ServiceFabric.Services.Runtime
open Microsoft.ServiceFabric.Services.Communication.Runtime
open Microsoft.ServiceFabric.Services.Runtime
open System.Collections.Generic
open System.Linq
open System.Diagnostics

module Async = 
    let inline awaitPlainTask (task : Task) = 
        // rethrow exception from preceding task if it fauled 
        let continuation (t : Task) : unit = 
            match t.IsFaulted with
            | true -> raise t.Exception
            | arg -> ()
        task.ContinueWith continuation |> Async.AwaitTask
    
    let inline startAsPlainTask (work : Async<unit>) = Task.Factory.StartNew(fun () -> work |> Async.RunSynchronously)

type SuaveService() as self = 
    inherit Microsoft.ServiceFabric.Services.Runtime.StatelessService()
    //let mutable port = 8583 // default port
    let buildConfig portToUse ct = 
        { defaultConfig with 
                            cancellationToken  = ct
                            bindings = 
                                 [ { defaultConfig.bindings.Head with socketBinding = 
                                                                          { defaultConfig.bindings.Head.socketBinding with port = 
                                                                                                                               uint16 
                                                                                                                                   portToUse } } ] }
    
    override __.CreateServiceInstanceListeners() : IEnumerable<ServiceInstanceListener> = 
        seq { 
            let factory (p : StatelessServiceInitializationParameters) : ICommunicationListener = 
                SuaveHost.ServiceEventSource.Current.ServiceMessage(self, "Suave-Factory Called")
                let home = choose [path "/" >>= GET >>= OK "Hello world"]
                let mind = choose [path "/mind" >>= GET >>= OK "Where is my mind?"]
                let app = choose [ home; mind ]
                let cancellationTokenSource = ref None
                let port = p.CodePackageActivationContext.GetEndpoint("SuaveEndpoint").Port
                let start() = 
                    let cts = new CancellationTokenSource()
                    let token = cts.Token
                    let config = buildConfig port token
                    
                    startWebServerAsync config app
                    |> snd
                    |> Async.StartAsTask 
                    |> ignore

                    cancellationTokenSource := Some cts
                    
                let stop() = 
                    match !cancellationTokenSource with
                    | Some cts -> cts.Cancel()
                    | None -> ()


                { new ICommunicationListener with
                      member __.Abort() = 
                        stop()
                        SuaveHost.ServiceEventSource.Current.ServiceMessage(self, "Suave-Abort Called")
                        ()
                      member __.CloseAsync _ = 
                        
                        SuaveHost.ServiceEventSource.Current.ServiceMessage(self, "Suave-Close Async Called")
                        stop()
                        Task.FromResult() :> Task
                      member __.OpenAsync cancellationToken = 
                          let listeningAddress  = sprintf "http://+:%d" port
                          let publishAddress = listeningAddress.Replace("+",FabricRuntime.GetNodeContext().IPAddressOrFQDN)
                          SuaveHost.ServiceEventSource.Current.ServiceMessage(self, "Suave-Open Async Called")
                          SuaveHost.ServiceEventSource.Current.ServiceMessage(self, "Suave-Listening Address-{0}",listeningAddress)
                          SuaveHost.ServiceEventSource.Current.ServiceMessage(self, "Suave-Publish Address-{0}",publishAddress)
                          start()
                          Task.FromResult(publishAddress)
                          }
            yield new ServiceInstanceListener(new System.Func<StatelessServiceInitializationParameters, ICommunicationListener>(factory))
        }

[<EntryPoint>]
let main argv = 
    try 
        use fabricRuntime = FabricRuntime.Create()
        fabricRuntime.RegisterServiceType("SuaveWebServiceType", typeof<SuaveService>)
        SuaveHost.ServiceEventSource.Current.ServiceTypeRegistered
            (Process.GetCurrentProcess().Id, typeof<SuaveService>.Name)
        Thread.Sleep Timeout.Infinite
    with e -> 
        SuaveHost.ServiceEventSource.Current.ServiceHostInitializationFailed(e.ToString())
        raise e
    0
