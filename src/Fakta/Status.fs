﻿module Fakta.Status

open Fakta
open Fakta.Impl

let statusDottedPath (funcName: string) =
  [| "Fakta"; "Status"; funcName |]

let internal statusPath (funcName: string) =
  [| "Fakta"; "Status"; funcName |]

let internal queryFilters state =
  statusPath >> queryFiltersNoMeta state

/// Leader is used to query for a known leader
let leader state: QueryCallNoMeta<string> =
  let createRequest =
    queryCall state.config "status/leader"

  let filters =
    queryFilters state "leader"
    >> codec createRequest fstOfJsonNoMeta

  HttpFs.Client.getResponse |> filters

/// Peers is used to query for a known raft peers
let peers state: QueryCallNoMeta<string list> =
  let createRequest =
    queryCall state.config "status/peers"

  let filters =
    queryFilters state "peers"
    >> codec createRequest fstOfJsonNoMeta

  HttpFs.Client.getResponse |> filters