﻿/************************************************************************************
 This implementation of the ElGamal encryption scheme is based on the code from [1].
 It was changed and extended by Vasily Sidorov (http://bazzilic.me/).
 
 This code is provided as-is and is covered by the WTFPL 2.0 [2] (except for the
 parts that belong by O'Reilly - they are covered by [3]).
 
 
 [1] Adam Freeman & Allen Jones, Programming .NET Security: O'Reilly Media, 2003,
     ISBN 9780596552275 (http://books.google.com.sg/books?id=ykXCNVOIEuQC)
 
 [2] WTFPL – Do What the Fuck You Want to Public License, website,
     (http://wtfpl.net/)
 
 [3] Tim O'Reilly, O'Reilly Policy on Re-Use of Code Examples from Books: website,
     2001, (http://www.oreillynet.com/pub/a/oreilly/ask_tim/2001/codepolicy.html)
 ************************************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ElGamalExt
{
    public class ElGamalManaged : ElGamal
    {
        private ElGamalKeyStruct o_key_struct;

        public ElGamalManaged()
        {            
            // create the key struct
            o_key_struct = new ElGamalKeyStruct();

            // set all of the big integers to zero
            o_key_struct.P = new BigInteger(0);
            o_key_struct.G = new BigInteger(0);
            o_key_struct.Y = new BigInteger(0);
            o_key_struct.X = new BigInteger(0);

            // set the default key size value
            KeySizeValue = 1024;

            // set the default padding mode
            Padding = ElGamalPaddingMode.Zeros;

            // set the range of legal keys
            LegalKeySizesValue = new KeySizes[] { new KeySizes(384, 1088, 8) };
        }

        public override string SignatureAlgorithm
        {
            get
            {
                return "ElGamal";
            }
        }

        public override string KeyExchangeAlgorithm
        {
            get
            {
                return "ElGamal";
            }
        }

        private void CreateKeyPair(int p_key_strength)
        {
            // create the random number generator
            Random x_random_generator = new Random();

            // create the large prime number, P
            o_key_struct.P = BigInteger.genPseudoPrime(p_key_strength,
                16, x_random_generator);

            // create the two random numbers, which are smaller than P
            o_key_struct.X = new BigInteger();
            o_key_struct.X.genRandomBits(p_key_strength - 1, x_random_generator);
            o_key_struct.G = new BigInteger();
            o_key_struct.G.genRandomBits(p_key_strength - 1, x_random_generator);

            // compute Y
            o_key_struct.Y = o_key_struct.G.modPow(o_key_struct.X, o_key_struct.P);
        }

        private bool NeedToGenerateKey()
        {
            return o_key_struct.P == 0 && o_key_struct.G == 0 && o_key_struct.Y == 0;
        }

        public ElGamalKeyStruct KeyStruct
        {
            get
            {
                if (NeedToGenerateKey())
                {
                    CreateKeyPair(KeySizeValue);
                }
                return o_key_struct;
            }
            set
            {
                o_key_struct = value;
            }
        }

        public override void ImportParameters(ElGamalParameters p_parameters)
        {
            // obtain the  big integer values from the byte parameter values
            o_key_struct.P = new BigInteger(p_parameters.P);
            o_key_struct.G = new BigInteger(p_parameters.G);
            o_key_struct.Y = new BigInteger(p_parameters.Y);
            o_key_struct.Padding = p_parameters.Padding;

            if (p_parameters.X != null && p_parameters.X.Length > 0)
            {
                o_key_struct.X = new BigInteger(p_parameters.X);
            }

            // set the length of the key based on the import
            KeySizeValue = o_key_struct.P.bitCount();
        }

        public override ElGamalParameters ExportParameters(bool p_include_private_params)
        {
            if (NeedToGenerateKey())
            {
                // we need to create a new key before we can export 
                CreateKeyPair(KeySizeValue);
            }

            // create the parameter set
            ElGamalParameters x_params = new ElGamalParameters();

            // set the public values of the parameters
            x_params.P = o_key_struct.P.getBytes();
            x_params.G = o_key_struct.G.getBytes();
            x_params.Y = o_key_struct.Y.getBytes();
            x_params.Padding = o_key_struct.Padding;

            // if required, include the private value, X
            if (p_include_private_params)
            {
                x_params.X = o_key_struct.X.getBytes();
            }
            else
            {
                // ensure that we zero the value
                x_params.X = new byte[1];
            }
                        
            return x_params;
        }

        public override byte[] EncryptData(byte[] p_data)
        {
            if (NeedToGenerateKey())
            {
                // we need to create a new key before we can export 
                CreateKeyPair(KeySizeValue);
            }

            // encrypt the data
            ElGamalEncryptor x_enc = new ElGamalEncryptor(o_key_struct);
            
            return x_enc.ProcessData(p_data);
        }

        public override byte[] DecryptData(byte[] p_data)
        {
            if (NeedToGenerateKey())
            {
                // we need to create a new key before we can export 
                CreateKeyPair(KeySizeValue);
            }

            // encrypt the data
            ElGamalDecryptor x_enc = new ElGamalDecryptor(o_key_struct);
            
            return x_enc.ProcessData(p_data);
        }

        protected override void Dispose(bool p_bool)
        {
            // do nothing - no unmanaged resources to release
        }

        public override byte[] Sign(byte[] p_hashcode)
        {
            throw new System.NotImplementedException();
        }

        public override bool VerifySignature(byte[] p_hashcode, byte[] p_signature)
        {
            throw new System.NotImplementedException();
        }

        public override byte[] Multiply(byte[] p_first, byte[] p_second)
        {
            var blocksize = o_key_struct.getCiphertextBlocksize();

            if (p_first.Length != blocksize)
            {
                throw new System.ArgumentException("p_first", "Ciphertext to multiply should be exactly one block long.");
            }
            if (p_second.Length != blocksize)
            {
                throw new System.ArgumentException("p_second", "Ciphertext to multiply should be exactly one block long.");
            }

            Func<byte[], Tuple<BigInteger, BigInteger>> toBigIntegerPair = delegate(byte[] block)
            {
                // extract the byte arrays that represent A and B
                byte[] A_bytes = new byte[blocksize / 2];
                Array.Copy(block, 0, A_bytes, 0, A_bytes.Length);
                byte[] B_bytes = new byte[blocksize / 2];
                Array.Copy(block, A_bytes.Length, B_bytes, 0, B_bytes.Length);

                // create big integers from the byte arrays and return them
                return new Tuple<BigInteger, BigInteger>(new BigInteger(A_bytes), new BigInteger(B_bytes));
            };

            var a = toBigIntegerPair(p_first);
            var b = toBigIntegerPair(p_second);

            var c = new byte[blocksize];

            var cA = (a.Item1 * b.Item1) % o_key_struct.P;
            var cB = (a.Item2 * b.Item2) % o_key_struct.P;

            var cAbytes = cA.getBytes();
            var cBbytes = cB.getBytes();

            Array.Copy(cAbytes, 0, c, blocksize / 2 - cAbytes.Length, cAbytes.Length);
            Array.Copy(cBbytes, 0, c, blocksize - cBbytes.Length, cBbytes.Length);

            return c;
        }
    }
}
