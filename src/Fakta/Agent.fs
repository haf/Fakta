﻿///The Agent endpoints are used to interact with the local Consul agent.
/// Usually, services and checks are registered with an agent which then takes
/// on the burden of keeping that data synchronized with the cluster. For
/// example, the agent registers services and checks with the Catalog and
/// performs anti-entropy to recover from outages.
module Fakta.Agent

open System
open System.Text
open Fakta
open Fakta.Logging
open Fakta.Impl
open System
open NodaTime
open HttpFs.Client
open Aether
open Aether.Operators
open Chiron
open Hopac

let agentPath (funcName: string) =
  [| "Fakta"; "Agent"; funcName |]

let writeFilters state =
  agentPath >> writeFilters state

let queryFilters state =
  agentPath >> queryFilters state

let checkDeregister state : WriteCall<Id, unit> =
  let createRequest =
    writeCallEntity state.config "agent/check/deregister"

  let filters =
    writeFilters state "checkDeregister"
    >> codec createRequest hasNoRespBody

  HttpFs.Client.getResponse |> filters

/// The register endpoint is used to add a new check to the local agent. There
/// is more documentation on checks here. Checks may be of script, HTTP, TCP, or
/// TTL type. The agent is responsible for managing the status of the check and
/// keeping the Catalog in sync.
///
/// This endpoint supports ACL tokens. If the query string includes a
/// ?token=<token-id>, the registration will use the provided token to authorize
/// the request. The token is also persisted in the agent's local configuration
/// to enable periodic anti-entropy syncs and seamless agent restarts.
let checkRegister state : WriteCall<AgentCheckRegistration, unit> =
  let createRequest (registration, opts) =
    writeCallUri state.config "agent/check/register" opts
    |> basicRequest state.config Put
    |> withJsonBodyT registration

  let filters =
    writeFilters state "checkRegister"
    >> codec createRequest hasNoRespBody

  HttpFs.Client.getResponse |> filters

/// This endpoint is used to return all the checks that are registered with the
/// local agent. These checks were either provided through configuration files
/// or added dynamically using the HTTP API. It is important to note that the
/// checks known by the agent may be different from those reported by the
/// Catalog. This is usually due to changes being made while there is no leader
/// elected. The agent performs active anti-entropy, so in most situations
/// everything will be in sync within a few seconds.
let checks state : QueryCall<Map<string, AgentCheck>> =
  let createRequest =
    queryCall state.config "agent/checks"

  let filters =
    queryFilters state "checks"
    >> codec createRequest fstOfJson

  HttpFs.Client.getResponse |> filters

let private setNodeMaintenance state enable : WriteCall<string, unit> =
  let createRequest (reason, opts) =
    writeCall state.config "agent/maintenance" opts
    |> Request.queryStringItem "enable" (string enable |> String.toLowerInvariant)
    |> Request.queryStringItem "reason" reason

  let filters =
    writeFilters state "setNodeMaintenance"
    >> codec createRequest hasNoRespBody

  HttpFs.Client.getResponse |> filters

/// Sets the node in maintenance mode given a reason
let enableNodeMaintenance state =
  setNodeMaintenance state true

/// Sets the node in normal mode given a reason
let disableNodeMaintenance state =
  setNodeMaintenance state false

let private setServiceMaintenance state enable : WriteCall<Id * (*reason*) string, unit> =
  let createRequest ((service, reason), opts) =
    writeCallEntity state.config "agent/service/maintenance" (service, opts)
    |> Request.queryStringItem "enable" (string enable |> String.toLowerInvariant)
    |> Request.queryStringItem "reason" reason

  let filters =
    writeFilters state "setServiceMaintenance"
    >> codec createRequest hasNoRespBody

  HttpFs.Client.getResponse |> filters

/// The service maintenance endpoint allows placing a given service into
/// "maintenance mode". During maintenance mode, the service will be marked as
/// unavailable and will not be present in DNS or API queries. This API call is
/// idempotent. Maintenance mode is persistent and will be automatically
/// restored on agent restart.
///
/// The ?enable flag is required. Acceptable values are either true (to enter
/// maintenance mode) or false (to resume normal operation).
///
/// The ?reason flag is optional. If provided, its value should be a text string
/// explaining the reason for placing the service into maintenance mode. This is
/// simply to aid human operators. If no reason is provided, a default value
/// will be used instead.
let disableServiceMaintenance (state : FaktaState) : WriteCall<Id * (*reason*) string, unit> =
  setServiceMaintenance state false

/// The service maintenance endpoint allows placing a given service into
/// "maintenance mode". During maintenance mode, the service will be marked as
/// unavailable and will not be present in DNS or API queries. This API call is
/// idempotent. Maintenance mode is persistent and will be automatically
/// restored on agent restart.
///
/// The ?enable flag is required. Acceptable values are either true (to enter
/// maintenance mode) or false (to resume normal operation).
///
/// The ?reason flag is optional. If provided, its value should be a text string
/// explaining the reason for placing the service into maintenance mode. This is
/// simply to aid human operators. If no reason is provided, a default value
/// will be used instead.
let enableServiceMaintenance (state : FaktaState) : WriteCall<Id * (*reason*) string, unit> =
  setServiceMaintenance state true

/// This endpoint is hit with a GET and is used to instruct the agent to attempt
/// to connect to a given address. For agents running in server mode, providing
/// a "?wan=1" query parameter causes the agent to attempt to join using the WAN
/// pool.
let join state : WriteCall<(* address *) string * (* wan *) bool, unit> =
  let createRequest ((address, isWan), opts) =
    writeCallEntity state.config "agent/join" (address, opts)
    |> Request.queryStringItem "wan" (if isWan then "1" else "0")

  let filters =
    writeFilters state "join"
    >> codec createRequest hasNoRespBody

  HttpFs.Client.getResponse |> filters

/// This endpoint is used to return the members the agent sees in the cluster
/// gossip pool. Due to the nature of gossip, this is eventually consistent:
/// the results may differ by agent. The strongly consistent view of nodes is
// instead provided by "/v1/catalog/nodes".
/// For agents running in server mode, providing a "?wan=1" query parameter
/// returns the list of WAN members instead of the LAN members returned by
/// default.
let members state : QueryCall<bool, AgentMember list> =
  let createRequest (isWan, opts) =
    queryCall state.config "agent/members" opts
    |> Request.queryStringItem "wan" (if isWan then "1" else "0")
    
  let filters =
    queryFilters state "members"
    >> codec createRequest fstOfJson

  HttpFs.Client.getResponse |> filters

let self state : QueryCall<SelfData> =
  raise (TBD "finish Agent.self by writing FromJson and ToJson on SelfConfig")

let nodeName state : QueryCall<string> =
  let createRequest =
    queryCall state.config "agent/self"

  let nodeNameOptic =
    Json.Object_
    >?> Optics.Map.key_ "Config"
    >?> Json.Object_
    >?> Optics.Map.key_ "NodeName"
    
  let filters =
    queryFilters state "nodeName"
    >> codec createRequest (fstOfJsonPrism nodeNameOptic)

  HttpFs.Client.getResponse |> filters

/// ServiceDeregister is used to deregister a service with the local agent
let serviceDeregister (state : FaktaState) (serviceId : Id) : Job<Choice<unit, Error>> = job {
  let urlPath = (sprintf "check/deregister/%s" serviceId)
  let uriBuilder = UriBuilder.ofAgent state.config urlPath
  let! result = call state (agentPath "service.deregister") id uriBuilder HttpMethod.Put
  match result with
  | Choice1Of2 id ->
      return Choice1Of2 ()
  | Choice2Of2 err -> return Choice2Of2(err)
}

/// ServiceRegister is used to register a new service with the local agent
let serviceRegister (state : FaktaState) (service : AgentServiceRegistration) : Job<Choice<unit, Error>> = job {
  let urlPath = "service/register"
  let uriBuilder = UriBuilder.ofAgent state.config urlPath
  let serializedCheckReg = Json.serialize service
  let! result = call state (agentPath "service.register") (withJsonBody serializedCheckReg) uriBuilder HttpMethod.Put
  match result with
  | Choice1Of2 id ->
      return Choice1Of2 ()
  | Choice2Of2 err -> return Choice2Of2(err)
}


/// Services returns the locally registered services
let services (state : FaktaState) : Job<Choice<Map<string, AgentService>, Error>> = job {
  let urlPath = "services"
  let uriBuilder = UriBuilder.ofAgent state.config urlPath
  let! result = call state (agentPath urlPath) id uriBuilder HttpMethod.Get
  match result with
  | Choice1Of2 (body, (dur, resp)) ->
      let  item:Map<string,AgentService> = if body = "" then Map.empty else Json.deserialize (Json.parse body)
      return Choice1Of2 (item)
  | Choice2Of2 err -> return Choice2Of2(err)
}

/// UpdateTTL is used to update the TTL of a check
let updateTTL (state : FaktaState) (checkId : string) (note : string) (status : string) : Job<Choice<unit, Error>> = job {
  let urlPath = (sprintf  "check/%s/%s" status checkId)
  let uriBuilder = UriBuilder.ofAgent state.config urlPath
                    |> UriBuilder.mappendRange [ yield "note", Some(note) ]
  let checkUpdate = Json.serialize (CheckUpdate.GetUpdateJson status note )
  let! result = call state (agentPath (sprintf "check.%s" status)) (withJsonBody checkUpdate) uriBuilder HttpMethod.Put
  match result with
  | Choice1Of2 id ->
      return Choice1Of2 ()
  | Choice2Of2 err -> return Choice2Of2(err)
}

/// PassTTL is used to set a TTL check to the passing state
let passTTL (state : FaktaState) (checkId : string) (note : string) : Job<Choice<unit, Error>> =
  updateTTL state checkId note "pass"

/// WarnTTL is used to set a TTL check to the warning state
let warnTTL (state : FaktaState) (checkId : string) (note : string) : Job<Choice<unit, Error>> =
  updateTTL state checkId note "warn"

/// FailTTL is used to set a TTL check to the failing state
let failTTL (state : FaktaState) (checkId : string) (note : string) : Job<Choice<unit, Error>> =
  updateTTL state checkId note "fail"

/// ForceLeave is used to have the agent eject a failed node
let forceLeave (state : FaktaState) (node : string) : Job<Choice<unit, Error>> = job {
  let urlPath = (sprintf "force-leave/%s" node)
  let uriBuilder = UriBuilder.ofAgent state.config urlPath
  let! result = call state (agentPath "force-leave") id uriBuilder HttpMethod.Put
  match result with
    | Choice1Of2 id ->
        return Choice1Of2 ()
    | Choice2Of2 err -> return Choice2Of2(err)
}