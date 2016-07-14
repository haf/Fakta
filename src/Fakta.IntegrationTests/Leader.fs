﻿module Fakta.IntegrationTests.Leader

open Fuchu
open Fakta
open Fakta.Logging
open Fakta.Vault


[<Tests>]
let tests =
  testList "Vault leader tests" [
    testCase "sys.leader -> returns the high availability status and current leader instance of Vault" <| fun _ ->
      let listing = Leader.leader vaultState []
      ensureSuccess listing <| fun resp ->
        let logger = state.logger
        logger.logSimple (Message.sprintf [] "Leader address: %s Ha_enabled: %A is_self: %A" resp.leaderAddress resp.haEnabled resp.isSelf)
]