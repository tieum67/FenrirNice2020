#define CACHE_TO_DISK

#define DEBUG_SETUP

using System;
using System.Collections.Generic;
using System.Threading;
using JetBrains.Annotations;
using Sisus.Attributes;
using UnityEditor;

namespace Sisus
{
	/// <summary>
	/// Class that handles creating, caching and returning default drawer providers for inspectors.
	/// </summary>
	[InitializeOnLoad]
	public static class DefaultDrawerProviders
	{
		private static Dictionary<Type, IDrawerProvider> drawerProvidersByInspectorType = new Dictionary<Type, IDrawerProvider>(2);

		private static bool isReady;
		private static bool selfReady;

		private static bool threadedFullRebuildFinished;
		private static Dictionary<Type, IDrawerProvider> drawerProvidersByInspectorTypeRebuilt;

		#if CACHE_TO_DISK && (!NET_2_0 && !NET_2_0_SUBSET && !NET_STANDARD_2_0) // HashSet.GetObjectData is not implemented in older versions
		private static string SavePath()
		{
			return System.IO.Path.Combine(UnityEngine.Application.temporaryCachePath, "PowerInspector.DefaultDrawerProviders.data");
		}

		public static void Serialize()
		{
			using(var stream = new System.IO.MemoryStream())
			{
				var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
				formatter.Serialize(stream, drawerProvidersByInspectorType);
				System.IO.File.WriteAllBytes(SavePath(), stream.ToArray());
			}
		}

		public static void Deserialize()
		{
			var cachePath = SavePath();
			if(!System.IO.File.Exists(cachePath))
			{
				return;
			}

			try
			{
				var bytes = System.IO.File.ReadAllBytes(cachePath);
				using (var memStream = new System.IO.MemoryStream())
				{
					var binForm = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
					memStream.Write(bytes, 0, bytes.Length);
					memStream.Seek(0, System.IO.SeekOrigin.Begin);
					drawerProvidersByInspectorType = (Dictionary<Type, IDrawerProvider>)binForm.Deserialize(memStream);
				}

				#if DEV_MODE && PI_ASSERTATIONS
				UnityEngine.Debug.Assert(drawerProvidersByInspectorType != null);
				UnityEngine.Debug.Assert(drawerProvidersByInspectorType[typeof(PowerInspector)] != null);
				foreach(var drawerProvider in drawerProvidersByInspectorType.Values)
				{
					UnityEngine.Debug.Assert(drawerProvider.IsReady, drawerProvider.GetType());
				}
				#endif

				isReady = true;

				#if DEV_MODE && PI_ASSERTATIONS
				UnityEngine.Debug.Assert(IsReady, "DefaultDrawerProviders.IsReady");
				#endif
			}
			#if DEV_MODE
			catch(Exception e)
			{
			UnityEngine.Debug.LogWarning(e);
			#else
			catch
			{
			#endif
				if(drawerProvidersByInspectorType == null)
				{
					drawerProvidersByInspectorType = new Dictionary<Type, IDrawerProvider>(2);
				}
			}
		}
		#endif

		public static bool IsReady
		{
			get
			{
				if(!isReady)
				{
					if(!selfReady)
					{
						return false;
					}

					foreach(var provider in drawerProvidersByInspectorType.Values)
					{
						if(!provider.IsReady)
						{
							#if DEV_MODE
							UnityEngine.Debug.LogWarning("DefaultDrawerProviders.IsReady false because provider "+provider.GetType().Name+".IsReady=false");
							#endif
							return false;
						}
					}
					isReady = true;
				}

				return true;
			}
		}

		static DefaultDrawerProviders()
		{
			#if CACHE_TO_DISK && (!NET_2_0 && !NET_2_0_SUBSET && !NET_STANDARD_2_0) // HashSet.GetObjectData is not implemented in older versions
			if(System.IO.File.Exists(SavePath()))
			{
				Deserialize();
			}
			#endif

			EditorApplication.delayCall += Setup;
		}

		private static void Setup()
		{
			// This helps fix issue where OdinEditor has not yet been injected into Unity's internal systems before building custom editor types for inspected components.
			if(!ApplicationUtility.IsReady())
			{
				EditorApplication.delayCall += Setup;
				return;
			}

			// Wait until Inspector contents have been rebuilt using deserialized cached drawers
			// until moving on to fully rebuilding drawers from scratch.
			// This is because the process of building all the drawers can take a couple of seconds,
			// and we don't want to keep the user waiting for this duration.
			if(isReady)
			{
				foreach(var inspector in InspectorManager.Instance().ActiveInstances)
				{
					if(!inspector.SetupDone)
					{
						#if DEV_MODE && DEBUG_SETUP
						UnityEngine.Debug.Log("DefaultDrawerProviders - waiting until inspector Setup Done...");
						#endif
						EditorApplication.delayCall += Setup;
						return;
					}
				}
				#if DEV_MODE && DEBUG_SETUP
				UnityEngine.Debug.Log("Setup now done for all "+ InspectorManager.Instance().ActiveInstances.Count+" inspectors");
				#endif
			}

			// Make sure that Preferences have been fetched via AssetDatabase.LoadAssetAtPath before moving on to threaded code
			var preferences = InspectorUtility.Preferences;
			UnityEngine.Debug.Assert(preferences != null);

			ThreadPool.QueueUserWorkItem(SetupThreaded);

			ApplyRebuiltSetupDataWhenReady();
		}

		private static void ApplyRebuiltSetupDataWhenReady()
		{
			if(!threadedFullRebuildFinished)
			{
				EditorApplication.delayCall += ApplyRebuiltSetupDataWhenReady;
				return;
			}

			isReady = false;
			selfReady = true;

			if(!IsReady)
			{
				EditorApplication.delayCall += ApplyRebuiltSetupDataWhenReady;
				return;
			}

			#if DEV_MODE
			UnityEngine.Debug.Log("DefaultDrawerProviders - Applying rebuild setup data now!");
			#endif

			drawerProvidersByInspectorType = drawerProvidersByInspectorTypeRebuilt;
		}

		private static void SetupThreaded(object threadTaskId)
		{
			#if DEV_MODE
			var timer = new ExecutionTimeLogger();
			timer.Start("DefaultDrawerProviders.SetupThreaded");
			#endif

			drawerProvidersByInspectorTypeRebuilt = new Dictionary<Type, IDrawerProvider>(2);

			#if DEV_MODE
			timer.StartInterval("FindDrawerProviderForAttributesInTypes");
			#endif

			foreach(var type in TypeExtensions.GetAllTypesThreadSafe(false, true, true))
			{
				if(!typeof(IDrawerProvider).IsAssignableFrom(type))
				{
					continue;
				}

				foreach(var drawerProviderFor in AttributeUtility.GetAttributes<DrawerProviderForAttribute>(type))
				{
					var inspectorType = drawerProviderFor.inspectorType;
					if(inspectorType == null)
					{
						UnityEngine.Debug.LogError(drawerProviderFor.GetType().Name + " on class "+type.Name+" NullReferenceException - inspectorType was null!");
						continue;
					}

					IDrawerProvider drawerProvider;
					if(!drawerProvidersByInspectorTypeRebuilt.TryGetValue(inspectorType, out drawerProvider) || !drawerProviderFor.isFallback)
					{
						bool reusedExistingInstance = false;
						foreach(var createdDrawerProvider in drawerProvidersByInspectorTypeRebuilt.Values)
						{
							if(createdDrawerProvider.GetType() == type)
							{
								drawerProvidersByInspectorTypeRebuilt.Add(inspectorType, createdDrawerProvider);
								reusedExistingInstance = true;
								break;
							}
						}
						
						if(!reusedExistingInstance)
						{
							#if DEV_MODE && DEBUG_SETUP
							UnityEngine.Debug.Log("Creating new DrawerProvider instance of type "+type.Name+" for inspector"+inspectorType.Name);
							#endif

							#if DEV_MODE
							timer.StartInterval(type.Name+".CreateInstance");
							#endif

							var drawerProviderInstance = (IDrawerProvider)Activator.CreateInstance(type);

							#if DEV_MODE && PI_ASSERTATIONS
							UnityEngine.Debug.Assert(drawerProviderInstance != null);
							#endif

							drawerProvidersByInspectorTypeRebuilt.Add(inspectorType, drawerProviderInstance);

							#if DEV_MODE && PI_ASSERTATIONS
							UnityEngine.Debug.Assert(drawerProvidersByInspectorTypeRebuilt[inspectorType] != null);
							#endif

							#if DEV_MODE
							timer.FinishInterval();
							#endif
						}
					}
				}
			}

			#if DEV_MODE
			timer.FinishInterval();
			timer.StartInterval("Add derived types");
			#endif

			// Also add derived types of inspector types
			var addDerived = new List<KeyValuePair<Type, IDrawerProvider>>();
			foreach(var drawerByType in drawerProvidersByInspectorTypeRebuilt)
			{
				var exactInspectorType = drawerByType.Key;
				var derivedInspectorTypes = exactInspectorType.IsInterface ? TypeExtensions.GetImplementingTypes(exactInspectorType, true) : TypeExtensions.GetExtendingTypes(exactInspectorType, true);
				for(int n = derivedInspectorTypes.Length - 1; n >= 0; n--)
				{
					addDerived.Add(new KeyValuePair<Type, IDrawerProvider>(derivedInspectorTypes[n], drawerByType.Value));
				}
			}

			for(int n = addDerived.Count - 1; n >= 0; n--)
			{
				var add = addDerived[n];
				var derivedInspectorType = add.Key;
				if(!drawerProvidersByInspectorTypeRebuilt.ContainsKey(derivedInspectorType))
				{
					drawerProvidersByInspectorTypeRebuilt.Add(derivedInspectorType, add.Value);
				}
			}

			#if DEV_MODE && PI_ASSERTATIONS
			UnityEngine.Debug.Assert(drawerProvidersByInspectorTypeRebuilt[typeof(PowerInspector)] != null);
			#endif

			//selfReady = true;
			threadedFullRebuildFinished = true;

			#if DEV_MODE
			timer.FinishInterval();
			timer.FinishAndLogResults();
			#endif

			#if CACHE_TO_DISK && (!NET_2_0 && !NET_2_0_SUBSET && !NET_STANDARD_2_0) // HashSet.GetObjectData is not implemented in older versions
			EditorApplication.delayCall += Serialize;
			#endif
		}

		[CanBeNull]
		public static IDrawerProvider GetForInspector(Type inspectorType)
		{
			#if DEV_MODE && PI_ASSERTATIONS
			UnityEngine.Debug.Assert(IsReady);
			#endif

			IDrawerProvider drawerProvider;
			return drawerProvidersByInspectorType.TryGetValue(inspectorType, out drawerProvider) ? drawerProvider : null;
		}
	}
}