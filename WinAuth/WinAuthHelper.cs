﻿/*
 * Copyright (C) 2013 Colin Mackie.
 * This software is distributed under the terms of the GNU General Public License.
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using System.Windows;
using System.Windows.Forms;

using Org.BouncyCastle.Math;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;

using Microsoft.Win32;

using WinAuth.Resources;

namespace WinAuth
{
  /// <summary>
  /// Class proving helper functions to save data for application
  /// </summary>
  class WinAuthHelper
  {
    /// <summary>
    /// Registry key for application
    /// </summary>
    public const string WINAUTHREGKEY = @"Software\WinAuth3";

    /// <summary>
    /// Registry data name for last loaded file
    /// </summary>
    private const string WINAUTHREGKEY_LASTFILE = @"File{0}";

    /// <summary>
    /// Registry data name for last good
    /// </summary>
    private const string WINAUTHREGKEY_BACKUP = @"Backup";

		/// <summary>
		/// Registry data name for errors
		/// </summary>
		private const string WINAUTHREGKEY_ERROR = @"Error";

    /// <summary>
    /// Registry data name for saved skin
    /// </summary>
    private const string WINAUTHREGKEY_SKIN = @"Skin";

    /// <summary>
    /// Registry data name for machine time difference
    /// </summary>
    private const string WINAUTHREGKEY_TIMEDIFF = @"TimeDiff";

    /// <summary>
    /// Registry data name for beta key
    /// </summary>
    private const string WINAUTHREGKEY_BETAWARNING = @"BetaWarning";

    /// <summary>
    /// Registry key for starting with windows
    /// </summary>
    private const string RUNKEY = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// Name of application config file for 1.3
    /// </summary>
    public const string CONFIG_FILE_NAME_1_3 = "winauth.xml";

    /// <summary>
    /// Name of default authenticator file
    /// </summary>
    public const string DEFAULT_AUTHENTICATOR_FILE_NAME = "winauth.xml";

    /// <summary>
    /// Number of password attempts
    /// </summary>
    private const int MAX_PASSWORD_RETRIES = 3;

    /// <summary>
    /// Number of saved authenticators
    /// </summary>
    private const int MAX_SAVED_FILES = 5;

    /// <summary>
    /// The WinAuth PGP public key
    /// </summary>
    public const string WINAUTH_PGP_PUBLICKEY =
      @"-----BEGIN PGP PUBLIC KEY BLOCK-----
			Version: BCPG C# v1.7.4114.6375
			
			mQEMBFA8sxQBCAC5EWjbGHDgyo4e9rcwse1mWbCOyeTwGZH2malJreF2v81KwBZa
			eCAPX6cP6EWJPlMOgkJpBQOgh+AezkYEidrW4+NXCGv+Z03U1YBc7e/nYnABZrJx
			XsqWVyM3d3iLSpKsMfk2OAIAIvoCvzcdx0ljm2IXGKRHGnc0nU7hSFXh5S/sJErN
			Cgrll6lD2CPNIPuUiMSWptgO1RAjerk0rwLh1DSChicPMJZfxJWn7JD1VVQLmAon
			EJ4x0MUIbff7ZmEna4O2rF9mrCjwfANkcz8N6WFp3PrfhxArXkvOBPYF9iEigFRS
			QVt6XAF6sjGhSYxZRaRj0tE4PyajE/HfNk0DAAkBAbQbV2luQXV0aCA8d2luYXV0
			aEBnbWFpbC5jb20+iQE0BBABAgASBQJQPRWEApsPAhYBAhUCAgsDABYJEJ3DDyNp
			qwwqApsPAhYBAhUCAgsDqb8IAKJRlRu5ne2BuHrMlKW/BI6I/hpkGQ8BzmO7LatM
			YYj//XKkbQ2BSvbDNI1al5HSo1iseokIZqD07iMwsp9GvLXSOVCROK9HYJ4dHsdP
			l68KgNDWu8ZDhPRGerf4+pn1jRfXW4NdFT8W1TX3RArpdVSd5Q2tV2tZrANErBYa
			UTDodsNKwikcgk89a2NI+Lh17lFGCFdAdZ07gRwu6cOm4SqP2TjWjDreXqlE9fHd
			0dwmYeS1QlGYK3ETNS1KvVTNaKdht231jGwlxy09Rxtx1EBLqFNsc+BW5rjYEPN2
			EAlelUJsVidUjZNB1ySm9uW8xurSEXWPZxWITl+LYmgwtn0=
			=dvwu
			-----END PGP PUBLIC KEY BLOCK-----";


    /// <summary>
    /// Load the authenticator and configuration settings
    /// </summary>
    /// <param name="form">parent winform</param>
    /// <param name="configFile">name of configfile or null for auto</param>
    /// <param name="password">optional supplied password or null to prompt if necessatu</param>
    /// <returns>new WinAuthConfig settings</returns>
    public static WinAuthConfig LoadConfig(Form form, string configFile, string password = null)
    {
      WinAuthConfig config = new WinAuthConfig();

			//if (string.IsNullOrEmpty(configFile) == true)
			//{
			//	configFile = GetLastFile(1);
			//	if (string.IsNullOrEmpty(configFile) == false && File.Exists(configFile) == false)
			//	{
			//		// ignore it if file does't exist
			//		configFile = null;
			//	}
			//}
      if (string.IsNullOrEmpty(configFile) == true)
      {
        // check for file in current directory
        configFile = Path.Combine(Environment.CurrentDirectory, DEFAULT_AUTHENTICATOR_FILE_NAME);
        if (File.Exists(configFile) == false)
        {
          configFile = null;
        }
      }
      if (string.IsNullOrEmpty(configFile) == true)
      {
        // check for file in exe directory
        configFile = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), DEFAULT_AUTHENTICATOR_FILE_NAME);
        if (File.Exists(configFile) == false)
        {
          configFile = null;
        }
      }
      if (string.IsNullOrEmpty(configFile) == true)
      {
        // do we have a file specific in the registry?
        string configDirectory = Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), WinAuthMain.APPLICATION_NAME);
        // check for default authenticator
        configFile = Path.Combine(configDirectory, DEFAULT_AUTHENTICATOR_FILE_NAME);
        // if no config file, just return a blank config
        if (File.Exists(configFile) == false)
        {
          return config;
        }
      }

      // if no config file when one was specified; report an error
      if (File.Exists(configFile) == false)
      {
        MessageBox.Show(form,
          strings.CannotFindConfigurationFile + ": " + configFile,
          form.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        return config;
      }

			try
			{
				using (FileStream fs = new FileStream(configFile, FileMode.Open, FileAccess.Read))
				{
					XmlReader reader = XmlReader.Create(fs);
					config.ReadXml(reader, password);
				}

				config.Filename = configFile;

				if (config.Version < WinAuthConfig.CURRENTVERSION)
				{
					FileInfo fi = new FileInfo(configFile);
					foreach (WinAuthAuthenticator wa in config)
					{
						wa.Created = fi.CreationTime;
					}
					//config.PasswordType = Authenticator.PasswordTypes.Explicit;
					//config.Password = "test";
					SaveConfig(config);
				}
				//else
				//{
				//	SaveConfig(form, configFile, config);
				//}
			}
			catch (EncrpytedSecretDataException )
			{
				// we require a password
				throw;
			}
			catch (BadPasswordException)
			{
				// we require a password
				throw;
			}
			catch (Exception ex)
			{
				MessageBox.Show(form, string.Format(strings.CannotLoadAuthenticator, configFile) + ": " + ex.Message, "Load Authenticator", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}

      SaveToRegistry(config);

      return config;
    }

    /// <summary>
    /// Save the authenticator
    /// </summary>
    /// <param name="configFile">filename to save to</param>
    /// <param name="config">current settings to save</param>
    public static void SaveConfig(WinAuthConfig config)
    {
      // create the xml
      XmlWriterSettings settings = new XmlWriterSettings();
      settings.Indent = true;
      settings.Encoding = Encoding.UTF8;

      // Issue 41 (http://code.google.com/p/winauth/issues/detail?id=41): saving may crash leaving file corrupt, so write into memory stream first before an atomic file write
      using (MemoryStream ms = new MemoryStream())
      {
        // save config into memory
        using (XmlWriter writer = XmlWriter.Create(ms, settings))
        {
          config.WriteXmlString(writer);
        }

				// if no config file yet, use defaault
				if (string.IsNullOrEmpty(config.Filename) == true)
				{
					string configDirectory = Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), WinAuthMain.APPLICATION_NAME);
					Directory.CreateDirectory(configDirectory);
					config.Filename = Path.Combine(configDirectory, DEFAULT_AUTHENTICATOR_FILE_NAME);
				}

        // write memory stream to file
        byte[] data = ms.ToArray();
				using (FileStream fs = new FileStream(config.Filename, FileMode.Create, FileAccess.Write, FileShare.None))
        {
          fs.Write(data, 0, data.Length);
        }
      }
    }

    /// <summary>
    /// Save a PGP encrpyted version of the config into the registry for recovery
    /// </summary>
    /// <param name="config"></param>
		public static void SaveToRegistry(WinAuthConfig config)
    {
      if (config == null)
      {
        return;
      }

      using (RegistryKey key = Registry.CurrentUser.CreateSubKey(WINAUTHREGKEY))
      {
        using (RegistryKey subkey = key.CreateSubKey(WINAUTHREGKEY_BACKUP))
        {
					// create an unencrypted clone
					config = config.Clone() as WinAuthConfig;
					config.PasswordType = Authenticator.PasswordTypes.None;
					foreach (var wa in config)
          {
						//wa.PasswordType = Authenticator.PasswordTypes.None;
						//if (wa.AuthenticatorData.EncryptedData == null)
						//{
						//	wa.AuthenticatorData.PasswordType = Authenticator.PasswordTypes.None;
						//}
					}

					// save the whole config
          using (StringWriter sw = new StringWriter())
          {
            XmlWriterSettings xmlsettings = new XmlWriterSettings();
            xmlsettings.Indent = true;
            using (XmlWriter xw = XmlWriter.Create(sw, xmlsettings))
            {
							config.WriteXmlString(xw, true);
            }
            subkey.SetValue("Config", PGPEncrypt(sw.ToString(), WinAuthHelper.WINAUTH_PGP_PUBLICKEY));
          }
        }
      }
    }

		/// <summary>
		/// Save a PGP encrpyted version of an authenticator into the registry for recovery
		/// </summary>
		/// <param name="wa">WinAuthAuthenticator instance</param>
		public static void SaveToRegistry(WinAuthAuthenticator wa)
		{
			if (wa == null || wa.AuthenticatorData == null)
			{
				return;
			}

			using (RegistryKey key = Registry.CurrentUser.CreateSubKey(WINAUTHREGKEY))
			{
				using (RegistryKey subkey = key.CreateSubKey(WINAUTHREGKEY_BACKUP))
				{
					using (SHA256 sha = new SHA256Managed())
					{
						// get a hash based on the authenticator key
						string authkey = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(wa.AuthenticatorData.SecretData)));

						// save the PGP encrypted key
						using (StringWriter sw = new StringWriter())
						{
							XmlWriterSettings xmlsettings = new XmlWriterSettings();
							xmlsettings.Indent = true;
							using (XmlWriter xw = XmlWriter.Create(sw, xmlsettings))
							{
								wa.WriteXmlString(xw);
							}
							subkey.SetValue(authkey, PGPEncrypt(sw.ToString(), WinAuthHelper.WINAUTH_PGP_PUBLICKEY));
						}
					}
				}
			}
		}

		/// <summary>
		/// Save a PGP encrpyted version of an error into the registry for diagnostics
		/// </summary>
		public static void SaveLastErrorToRegistry(string error)
		{
			if (error == null)
			{
				return;
			}

			using (RegistryKey key = Registry.CurrentUser.CreateSubKey(WINAUTHREGKEY))
			{
				using (RegistryKey subkey = key.CreateSubKey(WINAUTHREGKEY_BACKUP))
				{
					// save the PGP encrypted key
					subkey.SetValue(WINAUTHREGKEY_ERROR, PGPEncrypt(error, WinAuthHelper.WINAUTH_PGP_PUBLICKEY));
				}
			}
		}

		/// <summary>
		/// Read the encrpyted backup registry entries to be sent within the diagnostics report
		/// </summary>
		public static string ReadBackupFromRegistry()
		{
			StringBuilder buffer = new StringBuilder();
			using (RegistryKey key = Registry.CurrentUser.OpenSubKey(WINAUTHREGKEY))
			{
				if (key != null)
				{
					using (RegistryKey subkey = key.OpenSubKey(WINAUTHREGKEY_BACKUP))
					{
						if (subkey != null)
						{
							foreach (string name in subkey.GetValueNames())
							{
								object val = subkey.GetValue(name);
								if (val != null)
								{
									buffer.Append(name + "=" + Convert.ToString(val)).Append(Environment.NewLine);
								}
							}
						}
					}
				}
			}

			return buffer.ToString();
		}

		/// <summary>
    /// Set up winauth so it will start with Windows by adding entry into registry
    /// </summary>
    /// <param name="enabled">enable or disable start with windows</param>
    public static void SetStartWithWindows(bool enabled)
    {
      using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RUNKEY, true))
      {
        if (enabled == true)
        {
          // get path of exe and minimize flag
          key.SetValue(WinAuthMain.APPLICATION_NAME, Application.ExecutablePath + " -min");
        }
        else if (key.GetValue(WinAuthMain.APPLICATION_NAME) != null)
        {
          key.DeleteValue(WinAuthMain.APPLICATION_NAME);
        }
      }
    }

    /// <summary>
    /// Get the saved skin file name from the registry
    /// </summary>
    /// <returns>name of skin file or null if none</returns>
    public static string GetSavedSkin()
    {
      using (RegistryKey key = Registry.CurrentUser.OpenSubKey(WINAUTHREGKEY, false))
      {
        return (key == null ? null : key.GetValue(WINAUTHREGKEY_SKIN, null) as string);
      }
    }

    /// <summary>
    /// Set or remove the saved skin in the registry
    /// </summary>
    /// <param name="skin">name of skin file or null to remove</param>
    public static void SetSavedSkin(string skin)
    {
      using (RegistryKey key = Registry.CurrentUser.CreateSubKey(WINAUTHREGKEY))
      {
        if (string.IsNullOrEmpty(skin) == false)
        {
          key.SetValue(WINAUTHREGKEY_SKIN, skin);
        }
        else
        {
          key.DeleteValue(WINAUTHREGKEY_SKIN, false);
        }
      }
    }

    /// <summary>
    /// Get the difference for the time of the local machine from the servers
    /// </summary>
    /// <returns>name of skin file or null if none</returns>
    public static long GetMachineTimeDiff()
    {
      using (RegistryKey key = Registry.CurrentUser.OpenSubKey(WINAUTHREGKEY, false))
      {
        return (key == null ? 0L : (long)key.GetValue(WINAUTHREGKEY_TIMEDIFF, 0L));
      }
    }

    /// <summary>
    /// Set the time difference in the registry
    /// </summary>
    /// <param name="skin">name of skin file or null to remove</param>
    public static void SetMachineTimeDiff(long diff)
    {
      using (RegistryKey key = Registry.CurrentUser.CreateSubKey(WINAUTHREGKEY))
      {
        key.SetValue(WINAUTHREGKEY_TIMEDIFF, diff, RegistryValueKind.QWord);
      }
    }

#if BETA
		/// <summary>
		/// Display a beta warning and return if true if we accepted. Beta version set in registry so
		/// we don't have to show again.
		/// </summary>
		/// <returns>true if beta accetped</returns>
		public static bool BetaWarning(Form form)
		{
			// Show if BETA
			string betaversion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
			using (RegistryKey key = Registry.CurrentUser.CreateSubKey(WINAUTHREGKEY))
			{
				string betaConfirmed = (key == null ? null : key.GetValue(WINAUTHREGKEY_BETAWARNING, null) as string);
				if (string.Compare(betaConfirmed, betaversion) != 0)
				{
					if (new BetaForm().ShowDialog(form) != DialogResult.OK)
					{
						return false;
					}

					key.SetValue(WINAUTHREGKEY_BETAWARNING, betaversion);
				}
			}

			return true;
		}
#endif

    /// <summary>
    /// Build a PGP key pair
    /// </summary>
    /// <param name="bits">number of bits in key, e.g. 2048</param>
    /// <param name="identifier">key identifier, e.g. "Your Name <your@emailaddress.com>" </param>
    /// <param name="password">key password or null</param>
    /// <param name="privateKey">returned ascii private key</param>
    /// <param name="publicKey">returned ascii public key</param>
    public static void PGPGenerateKey(int bits, string identifier, string password, out string privateKey, out string publicKey)
    {
      // generate a new RSA keypair 
      RsaKeyPairGenerator gen = new RsaKeyPairGenerator();
      gen.Init(new RsaKeyGenerationParameters(BigInteger.ValueOf(0x101), new Org.BouncyCastle.Security.SecureRandom(), bits, 80));
      AsymmetricCipherKeyPair pair = gen.GenerateKeyPair();

      // create PGP subpacket
      PgpSignatureSubpacketGenerator hashedGen = new PgpSignatureSubpacketGenerator();
      hashedGen.SetKeyFlags(true, PgpKeyFlags.CanCertify | PgpKeyFlags.CanSign | PgpKeyFlags.CanEncryptCommunications | PgpKeyFlags.CanEncryptStorage);
      hashedGen.SetPreferredCompressionAlgorithms(false, new int[] { (int)CompressionAlgorithmTag.Zip });
      hashedGen.SetPreferredHashAlgorithms(false, new int[] { (int)HashAlgorithmTag.Sha1 });
      hashedGen.SetPreferredSymmetricAlgorithms(false, new int[] { (int)SymmetricKeyAlgorithmTag.Cast5 });
      PgpSignatureSubpacketVector sv = hashedGen.Generate();
      PgpSignatureSubpacketGenerator unhashedGen = new PgpSignatureSubpacketGenerator();

      // create the PGP key
      PgpSecretKey secretKey = new PgpSecretKey(
        PgpSignature.DefaultCertification,
        PublicKeyAlgorithmTag.RsaGeneral,
        pair.Public,
        pair.Private,
        DateTime.Now,
        identifier,
        SymmetricKeyAlgorithmTag.Cast5,
        (password != null ? password.ToCharArray() : null),
        hashedGen.Generate(),
        unhashedGen.Generate(),
        new Org.BouncyCastle.Security.SecureRandom());

      // extract the keys
      using (MemoryStream ms = new MemoryStream())
      {
        using (ArmoredOutputStream ars = new ArmoredOutputStream(ms))
        {
          secretKey.Encode(ars);
        }
        privateKey = Encoding.ASCII.GetString(ms.ToArray());
      }
      using (MemoryStream ms = new MemoryStream())
      {
        using (ArmoredOutputStream ars = new ArmoredOutputStream(ms))
        {
          secretKey.PublicKey.Encode(ars);
        }
        publicKey = Encoding.ASCII.GetString(ms.ToArray());
      }
    }

    /// <summary>
    /// Encrypt string using a PGP public key
    /// </summary>
    /// <param name="plain">plain text to encrypt</param>
    /// <param name="armoredPublicKey">public key in ASCII "-----BEGIN PGP PUBLIC KEY BLOCK----- .. -----END PGP PUBLIC KEY BLOCK-----" format</param>
    /// <returns>PGP message string</returns>
    public static string PGPEncrypt(string plain, string armoredPublicKey)
    {
      // encode data
      byte[] data = Encoding.UTF8.GetBytes(plain);

      // create the WinAuth public key
      PgpPublicKey publicKey = null;
      using (MemoryStream ms = new MemoryStream(Encoding.ASCII.GetBytes(armoredPublicKey)))
      {
        using (Stream dis = PgpUtilities.GetDecoderStream(ms))
        {
          PgpPublicKeyRingBundle bundle = new PgpPublicKeyRingBundle(dis);
          foreach (PgpPublicKeyRing keyring in bundle.GetKeyRings())
          {
            foreach (PgpPublicKey key in keyring.GetPublicKeys())
            {
              if (key.IsEncryptionKey == true && key.IsRevoked() == false)
              {
                publicKey = key;
                break;
              }
            }
          }
        }
      }

      // encrypt the data using PGP
      using (MemoryStream encryptedStream = new MemoryStream())
      {
        using (ArmoredOutputStream armored = new ArmoredOutputStream(encryptedStream))
        {
          PgpEncryptedDataGenerator pedg = new PgpEncryptedDataGenerator(Org.BouncyCastle.Bcpg.SymmetricKeyAlgorithmTag.Cast5, true, new Org.BouncyCastle.Security.SecureRandom());
          pedg.AddMethod(publicKey);
          using (Stream pedgStream = pedg.Open(armored, new byte[4096]))
          {
            PgpCompressedDataGenerator pcdg = new PgpCompressedDataGenerator(Org.BouncyCastle.Bcpg.CompressionAlgorithmTag.Zip);
            using (Stream pcdgStream = pcdg.Open(pedgStream))
            {
              PgpLiteralDataGenerator pldg = new PgpLiteralDataGenerator();
              using (Stream encrypter = pldg.Open(pcdgStream, PgpLiteralData.Binary, "", (long)data.Length, DateTime.Now))
              {
                encrypter.Write(data, 0, data.Length);
              }
            }
          }
        }

        return Encoding.ASCII.GetString(encryptedStream.ToArray());
      }
    }

    /// <summary>
    /// Decrypt a PGP message (i.e. "-----BEGIN PGP MESSAGE----- ... -----END PGP MESSAGE-----")
    /// using the supplied private key.
    /// </summary>
    /// <param name="armoredCipher">PGP message to decrypt</param>
    /// <param name="armoredPrivateKey">PGP private key</param>
    /// <param name="keyPassword">PGP private key password or null if none</param>
    /// <returns>decrypted plain text</returns>
    public static string PGPDecrypt(string armoredCipher, string armoredPrivateKey, string keyPassword)
    {
      // decode the private key
      PgpPrivateKey privateKey = null;
      using (MemoryStream ms = new MemoryStream(Encoding.ASCII.GetBytes(armoredPrivateKey)))
      {
        using (Stream dis = PgpUtilities.GetDecoderStream(ms))
        {
          PgpSecretKeyRingBundle bundle = new PgpSecretKeyRingBundle(dis);
          foreach (PgpSecretKeyRing keyring in bundle.GetKeyRings())
          {
            foreach (PgpSecretKey key in keyring.GetSecretKeys())
            {
              privateKey = key.ExtractPrivateKey(keyPassword != null ? keyPassword.ToCharArray() : null);
              break;
            }
          }
        }
      }

      // decrypt armored block using our private key
      byte[] cipher = Encoding.ASCII.GetBytes(armoredCipher);
      using (MemoryStream decryptedStream = new MemoryStream())
      {
        using (MemoryStream inputStream = new MemoryStream(cipher))
        {
          using (ArmoredInputStream ais = new ArmoredInputStream(inputStream))
          {
            PgpObject message = new PgpObjectFactory(ais).NextPgpObject();
            if (message is PgpEncryptedDataList)
            {
              foreach (PgpPublicKeyEncryptedData pked in ((PgpEncryptedDataList)message).GetEncryptedDataObjects())
              {
                message = new PgpObjectFactory(pked.GetDataStream(privateKey)).NextPgpObject();
              }
            }
            if (message is PgpCompressedData)
            {
              message = new PgpObjectFactory(((PgpCompressedData)message).GetDataStream()).NextPgpObject();
            }
            if (message is PgpLiteralData)
            {
              byte[] buffer = new byte[4096];
              using (Stream stream = ((PgpLiteralData)message).GetInputStream())
              {
                int read;
                while ((read = stream.Read(buffer, 0, 4096)) > 0)
                {
                  decryptedStream.Write(buffer, 0, read);
                }
              }
            }

            return Encoding.UTF8.GetString(decryptedStream.ToArray());
          }
        }
      }
    }

  }
}
