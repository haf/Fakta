﻿module Fakta.Health
open Fakta
open Fakta.Logging
open Fakta.Impl
open System
open System.Collections
open NodaTime
open HttpFs.Client
open Chiron

let faktaHealthString = "Fakta.health"

let healthDottedPath (funcName: string) =
  (sprintf "%s.%s" faktaHealthString funcName)

let getValuesByName (action : string) (path : string) (state : FaktaState) (service : string) (opts : QueryOptions) 
  : Async<Choice<HealthCheck list * QueryMeta, Error>> =
    let urlPath = action
    let uriBuilder = UriBuilder.ofHealth state.config urlPath 
                     |> flip UriBuilder.mappendRange (queryOptKvs opts)
    let result = Async.RunSynchronously (call state path id uriBuilder HttpMethod.Get)

    async {
      match result with 
      | Choice1Of2 x -> 
         let body, (dur:Duration, resp:Response) = x
         let items = if body = "" then [] else Json.deserialize (Json.parse body)
         return Choice1Of2 (items, queryMeta dur resp)
      | Choice2Of2 err -> return Choice2Of2(err)
    }

/// Checks is used to return the checks associated with a service
let checks (state : FaktaState) (service : string) (opts : QueryOptions) : Async<Choice<HealthCheck list * QueryMeta, Error>> =
    getValuesByName ("checks/" + service)  (healthDottedPath "checks") state service opts 


/// Node is used to query for checks belonging to a given node
let node (state : FaktaState) (node : string) (opts : QueryOptions) : Async<Choice<HealthCheck list * QueryMeta, Error>> =
    getValuesByName ("node/" + node) (healthDottedPath "node") state node opts

let state (state : FaktaState) (endpointState : string) (opts : QueryOptions) : Async<Choice<HealthCheck list * QueryMeta, Error>> =
    getValuesByName ("state/" + endpointState) (healthDottedPath "state") state endpointState opts

/// Service is used to query health information along with service info for a given service. It can optionally do server-side filtering on a tag or nodes with passing health checks only.
let service (state : FaktaState) (service : string) (tag : string)
            (passingOnly : bool) (opts : QueryOptions)
            : Async<Choice<ServiceEntry list * QueryMeta, Error>> =
    let urlPath = ("service/" + service)
    let tagName, tagValue = if tag.Equals("") then "","" else "tag",tag
    let passingOnly = if passingOnly then "passing" else ""
    let uriBuilder = UriBuilder.ofHealth state.config ("service/" + service)
                     |> flip UriBuilder.mappendRange [ yield tagName, Some(tagValue) 
                                                       yield passingOnly, Some("") 
                                                       yield! queryOptKvs opts]
    let result = Async.RunSynchronously (call state (healthDottedPath "service") id uriBuilder HttpMethod.Get)
    async {
      match result with 
      | Choice1Of2 x -> 
         let body, (dur:Duration, resp:Response) = x
         let items = if body = "" then [] else Json.deserialize (Json.parse body)
         return Choice1Of2 (items, queryMeta dur resp)
      | Choice2Of2 err -> return Choice2Of2(err)
    }
  