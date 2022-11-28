﻿using System;
using System.Text;
using System.Threading.Tasks;
using Waher.Events;
using Waher.Events.Console;
using Waher.Events.MQTT;
using Waher.Networking.MQTT;
using Waher.Persistence;
using Waher.Persistence.Files;
using Waher.Runtime.Inventory.Loader;
using Waher.Runtime.Inventory;
using Waher.Runtime.Settings;
using Waher.Security.EllipticCurves;
using System.Collections.Generic;
using Waher.Security;

namespace Display
{
	/// <summary>
	/// This project implements a simple display that gets its values from information published via an MQTT Broker. 
	/// Information can be received in five different ways: Unstructured, Structured, Interoperable (first three unsecured), 
	/// and public, Confidential (last two secured).
	/// </summary>
	internal class Program
	{
		/// <summary>
		/// Program entry point
		/// </summary>
		static async Task Main()
		{
			FilesProvider DBProvider = null;
			MqttClient Mqtt = null;
			Edwards25519 Cipher;
			string DeviceID = string.Empty;

			try
			{
				// First, initialize environment and type inventory. This creates an inventory of types used by the application.
				// This is important for tasks such as data persistence, for example.
				TypesLoader.Initialize();

				// Setup Event Output to Console window
				Log.Register(new ConsoleEventSink());
				Log.Informational("Display application starting...");

				// Setup database
				Log.Informational("Setting up database...");
				DBProvider = await FilesProvider.CreateAsync("Database", "Default", 8192, 10000, 8192, Encoding.UTF8, 10000, true, false);
				Database.Register(DBProvider);

				// Repair database, if an inproper shutdown is detected
				await DBProvider.RepairIfInproperShutdown(string.Empty);

				// Starting internal modules
				await Types.StartAllModules(60000);

				// Configuring Device ID

				DeviceID = await RuntimeSettings.GetAsync("Device.ID", string.Empty);

				if (string.IsNullOrEmpty(DeviceID))
				{
					Console.Out.WriteLine("Device ID has not been configured. Please provide the Device ID that will be used by this application.");

					DeviceID = UserInput("Device ID", DeviceID);
					await RuntimeSettings.SetAsync("Device.ID", DeviceID);
				}
				else
					Log.Informational("Using Device ID: " + DeviceID, DeviceID);

				// Configuring Keys

				string p = await RuntimeSettings.GetAsync("ed25519.p", string.Empty);
				byte[] Secret;

				if (string.IsNullOrEmpty(p))
					Secret = null;
				else
				{
					try
					{
						Secret = Convert.FromBase64String(p);
					}
					catch (Exception)
					{
						Secret = null;
					}
				}

				if (Secret is null)
				{
					Log.Informational("Generating new keys.", DeviceID);

					Cipher = new Edwards25519();
					Secret = Cipher.GenerateSecret();
					p = Convert.ToBase64String(Secret);
					await RuntimeSettings.SetAsync("ed25519.p", p);
				}

				Cipher = new Edwards25519(Convert.FromBase64String(p));

				Log.Informational("Public key: " + Convert.ToBase64String(Cipher.PublicKey), DeviceID);

				// Checking pairing information

				string PairedToKey = await RuntimeSettings.GetAsync("Pair.Ed25519.Public", string.Empty);
				string PairedToId = await RuntimeSettings.GetAsync("Pair.ID", string.Empty);
				byte[] PairedToBin;

				if (string.IsNullOrEmpty(PairedToKey))
					PairedToBin = null;
				else
				{
					try
					{
						PairedToBin = Convert.FromBase64String(PairedToKey);
					}
					catch
					{
						PairedToBin = null;
					}
				}

				if (PairedToBin is null)
					Log.Informational("Not paired to any device.", DeviceID);
				else
					Log.Informational("Paired to: " + PairedToKey + " (" + PairedToId + ")", DeviceID);

				// Configuring and connecting to MQTT Server

				bool MqttConnected = false;

				do
				{
					// MQTT Configuration

					string MqttHost = await RuntimeSettings.GetAsync("MQTT.Host", string.Empty);
					int MqttPort = (int)await RuntimeSettings.GetAsync("MQTT.Port", 8883);
					bool MqttEncrypted = await RuntimeSettings.GetAsync("MQTT.Tls", true);
					string MqttUserName = await RuntimeSettings.GetAsync("MQTT.UserName", string.Empty);
					string MqttPassword = await RuntimeSettings.GetAsync("MQTT.Password", string.Empty);

					if (string.IsNullOrEmpty(MqttHost))
					{
						Console.Out.WriteLine("MQTT host not configured. Please provide the connection details below. If the default value presented is sufficient, just press ENTER.");

						MqttHost = UserInput("MQTT Host", MqttHost);
						await RuntimeSettings.SetAsync("MQTT.Host", MqttHost);

						MqttPort = UserInput("MQTT Port", MqttPort, 1, 65535);
						await RuntimeSettings.SetAsync("MQTT.Port", MqttPort);

						MqttEncrypted = (MqttPort == 8883);
						MqttEncrypted = UserInput("Encrypt with TLS", MqttEncrypted);
						await RuntimeSettings.SetAsync("MQTT.Tls", MqttEncrypted);

						MqttUserName = UserInput("MQTT UserName", MqttUserName);
						await RuntimeSettings.SetAsync("MQTT.UserName", MqttUserName);

						MqttPassword = UserInput("MQTT Password", MqttPassword);
						await RuntimeSettings.SetAsync("MQTT.Password", MqttPassword);
					}

					if (!(Mqtt is null))
					{
						await Mqtt.DisposeAsync();
						Mqtt = null;
					}

					// Connecting to broker and waiting for connection to complete

					Log.Informational("Connecting to MQTT Broker...", DeviceID);

					TaskCompletionSource<bool> WaitForConnect = new TaskCompletionSource<bool>();

					Mqtt = new MqttClient(MqttHost, MqttPort, MqttEncrypted, MqttUserName, MqttPassword);

					Mqtt.OnConnectionError += (_, e) =>
					{
						Log.Error(e.Message, DeviceID);
						WaitForConnect.TrySetResult(false);
						return Task.CompletedTask;
					};

					Mqtt.OnError += (_, e) =>
					{
						Log.Error(e.Message, DeviceID);
						return Task.CompletedTask;
					};

					Mqtt.OnStateChanged += (_, NewState) =>
					{
						Log.Informational(NewState.ToString(), DeviceID);

						switch (NewState)
						{
							case MqttState.Offline:
							case MqttState.Error:
								WaitForConnect.TrySetResult(false);
								break;

							case MqttState.Connected:
								WaitForConnect.TrySetResult(true);
								break;
						}

						return Task.CompletedTask;
					};

					MqttConnected = await WaitForConnect.Task;
				}
				while (!MqttConnected);

				// Register MQTT event sink, allowing developer to follow what happens with devices.

				Log.Register(new MqttEventSink("MQTT Event Sink", Mqtt, "HardenMqtt/Events", true));
				Log.Informational("Display connected to MQTT.", DeviceID);

				// Configure CTRL+Z to close application gracefully.

				bool Continue = true;

				Console.CancelKeyPress += (_, e) =>
				{
					e.Cancel = true;
					Continue = false;
				};

				// Configure pairing

				if (PairedToBin is null)
				{
					Dictionary<int, string> NrToKey = new Dictionary<int, string>();
					Dictionary<string, string> KeyToDeviceId = new Dictionary<string, string>();

					// Receiving public key of device ready to be paired.

					Task CheckPairing(object sender, MqttContent e)
					{
						if (e.Topic.StartsWith("HardenMqtt/Secured/Pairing/Sensor/"))
						{
							lock (NrToKey)
							{
								string Key = e.Topic[34..];

								if (Key.Length < 100 && e.Data.Length < 100 && !KeyToDeviceId.ContainsKey(Key))
								{
									string RemoteDeviceId = Encoding.UTF8.GetString(e.Data);

									try
									{
										byte[] KeyBin = Convert.FromBase64String(Key);
										Cipher.GetSharedKey(KeyBin, Hashes.ComputeSHA256Hash);
									}
									catch
									{
										return Task.CompletedTask;  // Invalid key
									}

									int KeyNr = NrToKey.Count + 1;
									NrToKey[KeyNr] = Key;
									KeyToDeviceId[Key] = RemoteDeviceId;

									StringBuilder sb = new StringBuilder();

									sb.Append("Device ready to be paired: ");
									sb.Append(KeyNr);
									sb.Append(". Display, ");
									sb.Append(RemoteDeviceId);
									sb.Append(": ");
									sb.Append(Key);

									Log.Notice(sb.ToString(), DeviceID);
								}
							}
						}

						return Task.CompletedTask;
					};

					Mqtt.OnContentReceived += CheckPairing;

					// Subscribe to pairing messages

					await Mqtt.SUBSCRIBE("HardenMqtt/Secured/Pairing/Sensor/+");

					// Pair device

					while (PairedToBin is null)
					{
						PairedToKey = UserInput("Public Key of remote device", PairedToKey ?? string.Empty);

						try
						{
							if (int.TryParse(PairedToKey, out int Nr))
							{
								lock (NrToKey)
								{
									if (NrToKey.TryGetValue(Nr, out string SelectedKey) &&
										KeyToDeviceId.TryGetValue(SelectedKey, out string SelectedId))
									{
										PairedToKey = SelectedKey;
										PairedToId = SelectedId;
									}
								}
							}

							byte[] Bin = Convert.FromBase64String(PairedToKey);
							Edwards25519 Temp = new Edwards25519(Bin);

							PairedToBin = Bin;
							await RuntimeSettings.SetAsync("Pair.Ed25519.Public", PairedToKey);
							await RuntimeSettings.SetAsync("Pair.Id", PairedToId);

							Log.Informational("Pairing to " + PairedToKey + " (" + PairedToId + ")", DeviceID);
						}
						catch (Exception)
						{
							Log.Error("Invalid public key provided during pairing.", DeviceID);
						}
					}

					// Unsubscribe from pairing messages

					Mqtt.OnContentReceived -= CheckPairing;
					await Mqtt.UNSUBSCRIBE("HardenMqtt/Secured/Pairing/Sensor/+");
				}

				Mqtt.OnContentReceived += (sender, e) =>
				{
					if (e.Topic.StartsWith("HardenMqtt/Unsecured/Unstructured/") && e.Topic[34..] == PairedToId)
					{
						// Unstructured reception (unsecured)

					}
					else if (e.Topic.StartsWith("HardenMqtt/Unsecured/Structured/") && e.Topic[32..] == PairedToId)
					{
						// Structured reception (unsecured)

					}
					else if (e.Topic.StartsWith("HardenMqtt/Unsecured/Interoperable/") && e.Topic[35..] == PairedToId)
					{
						// Interoperable reception (unsecured)

					}
					else if (e.Topic.StartsWith("HardenMqtt/Unsecured/Public/") && e.Topic[28..] == PairedToKey)
					{
						// Interoperable, signed, public reception (secured)

					}
					else if (e.Topic.StartsWith("HardenMqtt/Unsecured/Confidential/") && e.Topic[34..] == PairedToKey)
					{
						// Interoperable, signed, confidential reception (secured)

					}

					return Task.CompletedTask;
				};

				await Mqtt.SUBSCRIBE(
					"HardenMqtt/Unsecured/Unstructured/" + PairedToId,
					"HardenMqtt/Unsecured/Structured/" + PairedToId,
					"HardenMqtt/Unsecured/Interoperable/" + PairedToId,
					"HardenMqtt/Unsecured/Public/" + PairedToKey,
					"HardenMqtt/Unsecured/Confidential/" + PairedToKey);

				// Normal operation

				Log.Informational("Display application started... Press CTRL+C to terminate the application.", DeviceID);

				while (Continue)
					await Task.Delay(100);
			}
			catch (Exception ex)
			{
				// Display exception terminating application
				Log.Alert(ex, DeviceID);
			}
			finally
			{
				// Shut down database gracefully

				Log.Informational("Display application stopping...", DeviceID);

				if (!(Mqtt is null))
				{
					await Mqtt.DisposeAsync();
					Mqtt = null;
				}

				if (!(DBProvider is null))
					await DBProvider.Flush();

				await Types.StopAllModules();
				Log.Terminate();
			}
		}

		#region UserInput methods

		/// <summary>
		/// Asks the user to input a string.
		/// </summary>
		/// <param name="Label">Label</param>
		/// <param name="Default">Default value</param>
		/// <returns>User input</returns>
		private static string UserInput(string Label, string Default)
		{
			Console.Out.Write(Label);

			if (!string.IsNullOrEmpty(Default))
			{
				Console.Out.Write(" (default: ");
				Console.Out.Write(Default);
				Console.Out.Write(")");
			}

			Console.Out.Write(": ");

			string s = Console.In.ReadLine();

			if (string.IsNullOrEmpty(s))
				return Default;
			else
				return s;
		}

		/// <summary>
		/// Asks the user to input a Boolean value.
		/// </summary>
		/// <param name="Label">Label</param>
		/// <param name="Default">Default value</param>
		/// <returns>User input</returns>
		private static bool UserInput(string Label, bool Default)
		{
			string s;
			bool Result;

			do
			{
				s = UserInput(Label, Default.ToString());
			}
			while (!bool.TryParse(s, out Result));

			return Result;
		}

		/// <summary>
		/// Asks the user to input a 32-bit integer.
		/// </summary>
		/// <param name="Label">Label</param>
		/// <param name="Default">Default value</param>
		/// <param name="Min">Smallest permitted value.</param>
		/// <param name="Max">Largest permitted value.</param>
		/// <returns>User input</returns>
		private static int UserInput(string Label, int Default, int Min, int Max)
		{
			string s;
			int Result;

			do
			{
				s = UserInput(Label, Default.ToString());
			}
			while (!int.TryParse(s, out Result) || Result < Min || Result > Max);

			return Result;
		}

		#endregion

	}
}
