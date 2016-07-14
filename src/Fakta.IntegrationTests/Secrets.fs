﻿module Fakta.IntegrationTests.Secrets

open System
open Fuchu
open Fakta
open Fakta.Logging
open Fakta.Vault

let genericSecretPath = "secret"
let consulSecretPath = "consul"
let pkiPath = "pki"

[<Tests>]
let testsGeneric =
  testList "Vault generic secrets tests" [
    testCase "sys.secret.write -> create a new secret" <| fun _ ->
      let data = Map.empty.Add("foo", "bar")
      let listing = Secrets.write initState ((data, genericSecretPath+"/secretOne"), [])
      ensureSuccess listing <| fun _ ->
        let logger = state.logger
        logger.logSimple (Message.sprintf [] "Secret created.")

    testCase "sys.secret.read-> read a secret" <| fun _ ->
      let listing = Secrets.read initState (genericSecretPath+"/secretOne", [])
      ensureSuccess listing <| fun sc ->
        let logger = state.logger
        logger.logSimple (Message.sprintf [] "Secret read: %A" sc)

    testCase "sys.secret.list-> get a list of secret's names" <| fun _ ->
      let listing = Secrets.list initState (genericSecretPath, [])
      ensureSuccess listing <| fun sc ->
        let logger = state.logger
        logger.logSimple (Message.sprintf [] "Secret list: %A" sc)

    testCase "sys.secret.delete-> delete a secret" <| fun _ ->
      let listing = Secrets.delete initState (genericSecretPath+"/secretOne", [])
      ensureSuccess listing <| fun _ ->
        let logger = state.logger
        logger.logSimple (Message.sprintf [] "Secret deleted.")
]

open System.Text

[<Tests>]
let testsConsul =
  testList "Vault consul secrets tests" [
    testCase "consul.config.access -> configure vault to know how to contact consul" <| fun _ ->
      let config = Map.empty.Add("token", ACL.tokenId).Add("address", state.config.serverBaseUri.ToString())
      let listing = Secrets.write initState ((config, consulSecretPath+"/config/access"), [])
      ensureSuccess listing <| fun _ ->
        let logger = state.logger
        logger.logSimple (Message.sprintf [] "Consul config sent to vault.")

    testCase "consul.roles -> create a new role" <| fun _ ->
      let policy64 =
        "read"
        |> UTF8Encoding.UTF8.GetBytes
        |> Convert.ToBase64String
      let config = Map.empty.Add("token_type", "management")
      let listing = Secrets.write initState ((config, consulSecretPath+"/roles/management"), [])
      ensureSuccess listing <| fun _ ->
        let logger = state.logger
        logger.logSimple (Message.sprintf [] "Consul role sent to vault.")


    testCase "consul.roles -> queries a consul role definiton" <| fun _ ->
      let listing = Secrets.read initState (consulSecretPath+"/roles/management", [])
      ensureSuccess listing <| fun sc ->
        let logger = state.logger
        logger.logSimple (Message.sprintf [] "Consul role read: %A" sc)
]

//[<Tests>]
//let testsPki =
//  testList "Vault pki cert secrets tests" [
//    testCase "pki.root.generate.internal -> generate root certificate" <| fun _ ->
//      let config = Map.empty.Add("common_name", "myvault.com").Add("ttl", "87600h")
//      let listing = Secrets.WriteWithReturnValue initState ((config, pkiPath+"/root/generate/internal"), [])
//      ensureSuccess listing <| fun sc ->
//        let logger = state.logger
//        logger.logSimple (Message.sprintf [] "Certigicate generated with return value: %A" sc)
//
//  ]