// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.


open System
open System.Fabric
open System.Threading
open OwinHost
open System.Diagnostics



[<EntryPoint>]
let main argv = 
    try 
        use fabricRuntime = FabricRuntime.Create()
        fabricRuntime.RegisterServiceType("WebServiceType", typeof<WebApiService>)
        OwinHost.ServiceEventSource.Current.ServiceTypeRegistered(Process.GetCurrentProcess().Id, typeof<WebApiService>.Name)
        Thread.Sleep Timeout.Infinite
    with e -> 
        OwinHost.ServiceEventSource.Current.ServiceHostInitializationFailed(e.ToString())
        raise e
    0
