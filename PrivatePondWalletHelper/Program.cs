using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace PrivatePondWalletHelper
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("This wizard will help you get your wallet config info for private pond. ");
            Network network = null;
            networkInput:
            Console.WriteLine("What bitcoin network? (mainnet|testnet)");
            var input = Console.ReadLine()?.ToLowerInvariant();
            if (input == "mainnet")
            {
                network = Network.Main;
            }
            else if (input == "testnet")
            {
                network = Network.TestNet;
            }
            else if (input == "regtest")
            {
                network = Network.RegTest;
            }
            else
            {
                Console.WriteLine("invalid value");
                goto networkInput;
            }

            ScriptPubKeyType kind = ScriptPubKeyType.Segwit;
            Console.WriteLine("What kind of wallet? (segwit|p2sh)");
            input = Console.ReadLine()?.ToLowerInvariant();
            if (input == "segwit")
            {
                kind = ScriptPubKeyType.Segwit;
            }
            else if (input == "p2sh")
            {
                kind = ScriptPubKeyType.SegwitP2SH;
            }

            type:
            Console.WriteLine("Provide seed or xpub? (seed|xpub)");
            input = Console.ReadLine()?.ToLowerInvariant();

            if (input == "seed")
            {
                seedEntry:
                Console.WriteLine("Enter seed");

                try
                {
                    var seed = new Mnemonic(Console.ReadLine()?.ToLowerInvariant());
                    Console.WriteLine("Is this intended for part of a multisig wallet? (y/n)");

                    input = Console.ReadLine()?.ToLowerInvariant();
                    KeyPath keyPath = null;
                    var multsig = false;
                    if (input == "y")
                    {
                        multsig = true;
                        var coinType = network == Network.Main ? "0'" : "1'";
                        var account = "0'";
                        var scriptType = kind == ScriptPubKeyType.Segwit ? "2'" : "1'";
                        keyPath = new KeyPath($"m/48'/{coinType}/{account}/{scriptType}");
                    }
                    else
                    {
                        var coinType = network == Network.Main ? "0'" : "1'";
                        var account = "0'";
                        var purpose = kind == ScriptPubKeyType.Segwit ? "84'" : "49'";

                        keyPath = new KeyPath($"m/{purpose}/{coinType}/{account}");
                    }

                    var rootedKeyPath =
                        new RootedKeyPath(seed.DeriveExtKey().GetPublicKey().GetHDFingerPrint(), keyPath);
                    var xpriv = seed.DeriveExtKey().Derive(keyPath);
                    Console.WriteLine($"Xpriv is {xpriv.GetWif(network)}");
                    Console.WriteLine($"Xpub is {xpriv.Neuter().GetWif(network)}");
                    
                    Console.WriteLine($"Derivation path is {keyPath}");
                    Console.WriteLine($"Fingerprint is {seed.DeriveExtKey().GetPublicKey().GetHDFingerPrint()}");
                    
                    Console.WriteLine($"Rooted Keypath is: {rootedKeyPath}");
                    if (!multsig)
                    {
                        Console.WriteLine($"Derivation scheme is {xpriv.Neuter().GetWif(network)}{(kind== ScriptPubKeyType.SegwitP2SH? "-[p2sh]": "")}");
                        
                    }
                    
                }
                catch (Exception)
                {
                    Console.WriteLine("Invalid seed");
                    goto seedEntry;
                }
            }
            else if (input == "xpub")
            {
                var mapping = network == Network.Main
                    ? new Dictionary<uint, ScriptPubKeyType>()
                    {
                        {0x0488b21eU, ScriptPubKeyType.Legacy}, // xpub
                        {0x049d7cb2U, ScriptPubKeyType.SegwitP2SH}, // ypub
                        {0x04b24746U, ScriptPubKeyType.Segwit}, //zpub
                    }
                    : new Dictionary<uint, ScriptPubKeyType>()
                    {
                        {0x043587cfU, ScriptPubKeyType.Legacy}, // tpub
                        {0x044a5262U, ScriptPubKeyType.SegwitP2SH}, // upub
                        {0x045f1cf6U, ScriptPubKeyType.Segwit} // vpub
                    };

                xpubEntry:

                Console.WriteLine("Enter xpub/ypub/zpub");
                try
                {
                    input = Console.ReadLine()?.Trim();
                    var base58Encoder = network.GetBase58CheckEncoder();
                    var data = base58Encoder.DecodeData(input);

                    var standardPrefix = Utils.ToBytes(
                        mapping.Single(pair => pair.Value == ScriptPubKeyType.Legacy).Key,
                        false);
                    for (int ii = 0; ii < 4; ii++)
                        data[ii] = standardPrefix[ii];
                    var coinType = network == Network.Main ? "0'" : "1'";
                    var account = "0'";
                    var purpose = kind == ScriptPubKeyType.Segwit ? "84'" : "49'";

                    var keyPath = new KeyPath($"m/{purpose}/{coinType}/{account}");
                    Console.WriteLine($"Derivation path is {keyPath}");

                    Console.WriteLine(
                        $"Xpub is {new BitcoinExtPubKey(base58Encoder.EncodeData(data), network).ToWif()}");
                    
                }
                catch (Exception)
                {
                    Console.WriteLine($"Invalid xpub");
                    goto xpubEntry;
                }
            }
            else
            {
                Console.WriteLine("invalid value");
                goto type;
            }

            if (kind == ScriptPubKeyType.SegwitP2SH)
            {
                Console.WriteLine("DON'T FORGET TO ADD -[p2sh] AT THE END FOR SEGWIT P2SH WALLETS!");
            }
            Console.WriteLine("Press any key to terminate.");
            Console.Read();
        }
    }
}