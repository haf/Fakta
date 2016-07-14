﻿module Fakta.IntegrationTests.GenerateRoot

open Fuchu
open Fakta
open Fakta.Logging
open Fakta.Vault
open System

let generateRootInitTest =
  let otp = 
    Array.init 16 (fun i -> byte(i*i))
    |> Convert.ToBase64String

  let listing = GenerateRoot.init vaultState (("otp", otp), [])
  ensureSuccess listing <| fun resp ->
    let logger = state.logger
    logger.logSimple (Message.sprintf [] "New root generation result: %A" resp)
    resp.nonce

let nonce = generateRootInitTest

[<Tests>]
let tests =
  testList "Vault root generation tests" [
    testCase "sys.generate-root.status -> get root generation status" <| fun _ ->
      let listing = GenerateRoot.status vaultState []
      ensureSuccess listing <| fun status ->
        let logger = state.logger
        logger.logSimple (Message.sprintf [] "Root generation status: %A" status)
    
    testCase "sys.generate-root.init -> initializes a new root generation attempt" <| fun _ ->
      nonce |> ignore

    testCase "sys.generate-root.cancel -> cancels any in-progress root generation attempt." <| fun _ ->
      let listing = GenerateRoot.cancel vaultState []
      ensureSuccess listing <| fun _ ->
        let logger = state.logger
        logger.logSimple (Message.sprintf [] "Root generations cancelled.")

//    testCase "sys.generate-root.update -> Update a master key" <| fun _ ->
//      let listing = GenerateRoot.Update initState ((initState.config.keys.Value.[0],nonce), [])
//      ensureSuccess listing <| fun resp ->
//        let logger = state.logger
//        logger.logSimple (Message.sprintf [] "New root generation result: %A" resp)
]