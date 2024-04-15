## Running dev env
* Install docker
* Install .net core sdk 
* run `docker-compose -f docker-compose.yml up`
* start PrivatePond project through launch settings profile
* The dev environment is configured to have 3 wallets:
* 2 wallets are single-signature hot wallets, configured to receive deposits to. 
* 1 wallet is a multi-sig cold wallet, configured to receive funds when the hot wallets are "overflowing"

## Running whole stack in docker
* `docker-compose -f docker-compose.yml -f docker-compose-run-locally.yml build --no-cache`
* `docker-compose -f docker-compose.yml -f docker-compose-run-locally.yml up`
* should become available at localhost:5000 
* You can view API docs at localhost:5000/swagger

## Helpers
* You can emulate new blocks by calling the `docker-bitcoin-generate.sh` pr `.ps1` files. `docker-bitcoin-generate.sh 6` generates 6 blocks for example
* You can use the bitcoin-cli sh/ps1 scripts to test sending funds.

## Wallet Helper
In order to aid you in configuring the wallets, there is a CLI tool which you can run on a secure machine to generate the wallet information if you do not have a source for it.

* Install .net core sdk 
* Go to `PrivatePondWalletHelper`
* Open a terminal/cmd and execute `dotnet run`
* fill in provided information and note what it prints out


## Configuring wallets

NON-SEGWIT wallets are not supported. They will causes errors in the system. 

Example configuration of one of the customer-facing wallets:
```
        "PP_PRIVATEPOND__WALLETS__0__DerivationScheme":  "tpubDCZB6sR48s4T5Cr8qHUYSZEFCQMMHRg8AoVKVmvcAP5bRw7ArDKeoNwKAJujV3xCPkBvXH5ejSgbgyN6kREmF7sMd41NdbuHa8n1DZNxSMg",
        "PP_PRIVATEPOND__WALLETS__0__AllowForDeposits":  "true",
        "PP_PRIVATEPOND__WALLETS__0__AllowForTransfers":  "true",        
        "PP_PRIVATEPOND__WALLETS__0__RootedKeyPaths__0":  "5c9e228d/m/84'/1'/0'",
```

You can define an unlimited number of wallets using configuration. There are 4 main properties to configuring a wallet,:
* `DerivationScheme` - an NBX format of a wallet
* `AllowForDeposits`- whether this wallet will allow users to diurectly deposit to it
* `AllowForTransfers`- whether this wallet will be used to do transfers to users (also requires it to be a hot wallet)
* `RootedKeyPaths__x` - the master fingerprint and keypath `FINGERPRINT/KEYPATH`

### It is recommended to mimic the same setup as the demo environment which consists of:
* 1 multi-sig native segwit wallet. This wallet never faces customers and is used to store the majority of funds. 
  * In this demo it is a 2 of 3, but can easily be bigger or smaller as per your needs.
* 2 hot wallets, one native segwit, and another segwit-p2sh. The native segwit provides better fees but is not as widely supported (mostly older, and bad wallets). P2sh walletys are full supported by all. This setup means that users will have a deposit option of both addresses at all times. 
  * Both wallets are hot wallets.
  * Both wallets are configured to be used to process user withdrawals

### Deposits
Deposit addresses are marked as inactive once they have been used. If a user re-uses one, their deposit will need to be approved by an administrator before the status switches to complete. Discourage address re-use is one of the most essential basics to achieving privacy.

### Transfers
In order to process pending external transfers, you must have hot wallets with `AllowForTransfers` set to `"true"`. Additionally you should set the option  `PP_PRIVATEPOND__BatchTransfersEvery` to a number of minutes of how often to process transfers. Recommended to be a 30 minute minimum.

## Hot-Cold wallet balancing
When transfers are processed, there is a feature to determine a percentage threshold of how much funds should in your wallets compared to a designated cold wallet (the multsig wallet in the demo).
You can set the cold wallet by configuring:
 * `PP_PRIVATEPOND__WalletReplenishmentSource` to the derivation scheme of the cold wallet. 
 * `PP_PRIVATEPOND__WalletReplenishmentIdealBalancePercentage` to the percentage that the cold wallet should hold in relation to the hot wallets.
 
When the hot wallets: 
 * Have more than the defined percentage - it sends funds to the cold wallet to reach the threshold
 * Have less than the defined percentage - it creates a signing request from the cold wallet to the hot wallets to reach the threshold. The cosigners of the cold wallet must sign this transaction before then next transfer processing or it will expire.    


### Express Transfers
In order for the system to allow express transfers, you must configure the following:
* `PP_PRIVATEPOND__EnableExternalExpressTransfers` to  `"true"`
Please note that the fee is not subtracted from the amount and you should deduct an appropriate estimate from your user balance system.


## Configuring a hot wallet
* You must set  the config item `PP_PRIVATEPOND__KEYSDIR` to a path where you wish to store the private keys
* If the path does not exist, the system will generate the directory.
* For every wallet you wish to create a hot wallet, create a file in the directory mentioned above with the name being the xpub of the wallet. Its contents should be the xpriv of the wallet.
* If it was correct, the system will generate an encrypted version of the file and delete the original within a few seconds.


## Payjoin Support
There is built in BIP78 Payjoin support, both for sending and receiving.

Receiving support is enabled by configuring:
* `PP_PRIVATEPOND__PayjoinEndpointRoute` to a customer facing web server that forwards the request To private pond's `pj` endpoint.
* `PP_PRIVATEPOND__EnablePayjoinDeposits` to `"true"`

### Transfer batching support
NOT CURRENTLY RECOMMENDED AS IT IS EXPERIMENTAL
There is early support for batching user withdrawal requests into a payjoin deposit by a customer, which significantly improves privacy for all parties by breaking heuristics. It is however, the first of its kind and still experimental and disabled by default. It is heavily encourages to test this extensively before activating in production.
Activate by
* `PP_PRIVATEPOND__BatchTransfersInPayjoin`  to `"true"`

Sending support when processing Express transfers is enabled by configuring:
* `PP_PRIVATEPOND__EnablePayjoinTransfers` to `"true"`
