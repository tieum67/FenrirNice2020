#define SAFE_MODE

//#define DEBUG_BUILD_DICTIONARIES_FOR_FIELDS
//#define DEBUG_BUILD_DICTIONARIES_FOR_ENUM_FIELDS
//#define DEBUG_BUILD_DICTIONARIES_FOR_DELEGATE_FIELDS
//#define DEBUG_BUILD_DICTIONARIES_FOR_UNITY_OBJECT_FIELDS
//#define DEBUG_BUILD_DICTIONARIES_FOR_CUSTOM_EDITORS
//#define DEBUG_BUILD_DICTIONARIES_FOR_PROPERTY_DRAWERS
//#define DEBUG_BUILD_DICTIONARIES_FOR_DECORATOR_DRAWERS
//#define DEBUG_BUILD_DICTIONARIES_FOR_NONFALLBACK_DRAWER
//#define DEBUG_BUILD_DICTIONARIES_FOR_COMPONENTS
//#define DEBUG_BUILD_DICTIONARIES_FOR_ASSETS
//#define DEBUG_BUILD_DICTIONARIES_FOR_PLUGINS

using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using Sisus.Attributes;
using Sisus.Compatibility;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sisus
{
	/// <summary>
	/// Default class responsible for determining which drawer should be used for which Unity Object and class member targets in inspectors
	/// </summary>
	[DrawerProviderFor(typeof(IInspector), true), Serializable]
	public class DefaultDrawerProvider : DrawerProviderBase
	{
		[CanBeNull]
		private static DefaultDrawerProvider instance;

		[NotNull]
		public static DefaultDrawerProvider Instance
		{
			get
			{
				if(instance == null)
				{
					instance = new DefaultDrawerProvider();
				}
				return instance;
			}
		}

		public DefaultDrawerProvider() : base()
		{

		}

		private static List<Type> AllDrawerTypes()
		{
			var drawerTypes = new List<Type>(256);
			foreach(var assembly in TypeExtensions.Assemblies)
			{
				var types = assembly.GetLoadableTypes(false);
				for(int n = types.Length - 1; n >= 0; n--)
				{
					var type = types[n];
					if(typeof(IDrawer).IsAssignableFrom(type) && !type.IsAbstract)
					{
						drawerTypes.Add(type);
					}
				}
			}
			return drawerTypes;
		}

		protected override void BuildDictionariesThreaded(DrawerProviderData drawersFor)
		{
			#if DEV_MODE
			var timer = new ExecutionTimeLogger();
			timer.Start("DefaultDrawerProvider.BuildDictionariesThreaded");
			#endif

			#if DEV_MODE
			timer.StartInterval("build list of all drawer types");
			#endif

			var drawerTypes = AllDrawerTypes();

			#if DEV_MODE
			timer.FinishInterval();
			timer.StartInterval("handle drawer from plugin providers");
			#endif

			drawersFor.customEditorAssetDefault = typeof(CustomEditorAssetDrawer);
			drawersFor.customEditorComponentDefault = typeof(CustomEditorComponentDrawer);
			drawersFor.componentDefault = typeof(ComponentDrawer);
			drawersFor.assetDefault = typeof(AssetDrawer);

			// Priority 1: plugin drawers providers
			var pluginProviders = DrawerFromPluginProvider.All;
			for(int n = pluginProviders.Length - 1; n >= 0; n--)
			{
				var provider = pluginProviders[n];

				if(!provider.IsActive)
				{
					#if DEV_MODE && DEBUG_BUILD_DICTIONARIES_FOR_PLUGINS
					Debug.Log("Skipping "+provider.GetType().Name+" because IsActive was "+StringUtils.False);
					#endif
					continue;
				}

				try
				{
					provider.AddFieldDrawer(drawersFor.fields);

					#if DEV_MODE && PI_ASSERTATIONS
					AssertAllGUIInstructionTypesImplement(drawersFor.fields.Values, typeof(IFieldDrawer), provider, "AddFieldDrawer");
					#endif

					provider.AddDecoratorDrawerDrawer(drawersFor.decoratorDrawers);
					
					#if DEV_MODE && PI_ASSERTATIONS
					AssertAllGUIInstructionTypesImplement(drawersFor.decoratorDrawers.Values, typeof(IDecoratorDrawerDrawer), provider, "AddDecoratorDrawerDrawer");
					#endif

					provider.AddPropertyDrawerDrawer(drawersFor.drawersByAttributeType, drawersFor.propertyDrawerDrawersByFieldType);
					
					#if DEV_MODE && PI_ASSERTATIONS
					foreach(var dictionary in drawersFor.drawersByAttributeType.Values)
					{
						AssertAllGUIInstructionTypesImplement(dictionary.Values, typeof(IPropertyDrawerDrawer), provider, "AddPropertyDrawerDrawer");
					}
					AssertAllGUIInstructionTypesImplement(drawersFor.propertyDrawerDrawersByFieldType.Values, typeof(IPropertyDrawerDrawer), provider, "AddPropertyDrawerDrawer");
					#endif
					
					provider.AddComponentDrawer(drawersFor.components);
					
					#if DEV_MODE && PI_ASSERTATIONS
					AssertAllGUIInstructionTypesImplementOne(drawersFor.decoratorDrawers.Values, typeof(IEditorlessComponentDrawer), typeof(ICustomEditorComponentDrawer), provider, "AddComponentDrawer");
					#endif

					provider.AddAssetDrawer(drawersFor.assets, drawersFor.assetsByExtension);

					#if DEV_MODE && PI_ASSERTATIONS
					AssertAllGUIInstructionTypesImplementOne(drawersFor.assets.Values, typeof(IEditorlessAssetDrawer), typeof(ICustomEditorAssetDrawer), provider, "AddAssetDrawer");
					AssertAllGUIInstructionTypesImplementOne(drawersFor.assetsByExtension.Values, typeof(IEditorlessAssetDrawer), typeof(ICustomEditorAssetDrawer), provider, "AddAssetDrawer");
					#endif
				}
				catch(Exception e)
				{
					Debug.LogError(e);
				}
			}

			#if DEV_MODE
			timer.FinishInterval();
			timer.StartInterval("exact types");
			#endif

			// Priority 2: non-fallback, non-inherited types
			BuildDictionariesForExactTypes(drawersFor, drawerTypes, false);

			#if DEV_MODE
			timer.FinishInterval();
			timer.StartInterval("inherited types");
			#endif

			// Priority 3: inherited types for non-fallback drawers
			BuildDictionariesForInheritedTypes(drawersFor, drawerTypes, false);
			
			#if DEV_MODE
			timer.FinishInterval();
			timer.StartInterval("property drawer, decorator drawer, custom editor");
			#endif

			#if UNITY_EDITOR
			// Priority 4: PropertyDrawers and DecoratorDrawers and Editors outside of Unity namespace
			var propertyDrawers = CustomEditorUtility.PropertyDrawersByType;
			var decoratorDrawers = CustomEditorUtility.DecoratorDrawersByType;
			var customEditors = CustomEditorUtility.CustomEditorsByType;
			BuildDictionariesForPropertyDrawersInNonUnityNamespaces(drawersFor, propertyDrawers);
			BuildDictionariesForDecoratorDrawersInNonUnityNamespaces(drawersFor, decoratorDrawers);
			BuildDictionariesForCustomEditorsInNonUnityNamespaces(drawersFor, customEditors);
			#endif
			
			#if DEV_MODE
			timer.FinishInterval();
			timer.StartInterval("fallback exact types");
			#endif

			//Priority 5: fallback, non-inherited types
			BuildDictionariesForExactTypes(drawersFor, drawerTypes, true); // stuck here!

			#if DEV_MODE
			timer.FinishInterval();
			timer.StartInterval("fallback inherited types");
			#endif

			//Priority 6: fallback, inherited types
			BuildDictionariesForInheritedTypes(drawersFor, drawerTypes, true);
			
			#if DEV_MODE
			timer.FinishInterval();
			#endif

			// Make sure that GameObjects have some drawer
			if(drawersFor.gameObject == null)
			{
				drawersFor.gameObject = typeof(GameObjectDrawer);
			}

			if(drawersFor.gameObjectCategorized == null)
			{
				drawersFor.gameObject = typeof(CategorizedGameObjectDrawer);
			}

			#if DEV_MODE
			timer.StartInterval("unity namespace property drawer, decorator drawer, custom editor");
			#endif

			#if UNITY_EDITOR
			// Priority 7: PropertyDrawer and DecoratorDrawers and Editors inside of Unity namespace
			BuildDictionariesForPropertyDrawersInUnityNamespaces(drawersFor, propertyDrawers);
			BuildDictionariesForDecoratorDrawersInUnityNamespaces(drawersFor, decoratorDrawers);
			BuildDictionariesForCustomEditorsInUnityNamespaces(drawersFor, customEditors);
			#endif

			#if DEV_MODE
			timer.FinishInterval();
			timer.FinishAndLogResults();
			#endif

			#if DEV_MODE && PI_ASSERTATIONS && UNITY_EDITOR
			Debug.Assert(!drawersFor.fields.ContainsValue(null));
			Debug.Assert(!drawersFor.fields.ContainsValue(typeof(PropertyDrawerDrawer)), "drawersFor.fields contained a PropertyDrawerDrawer. They should be in drawersFor.propertyDrawersByFieldType instead.");
			Debug.Assert(!drawersFor.fields.ContainsValue(typeof(DecoratorDrawerDrawer)), "drawersFor.fields contained a DecoratorDrawerDrawer.  They should be in drawersFor.decoratorDrawers instead.");

			Debug.Assert(!drawersFor.decoratorDrawers.ContainsValue(null));
			Debug.Assert(!drawersFor.decoratorDrawers.ContainsValue(typeof(PropertyDrawerDrawer)));
			foreach(var propertyDrawerDrawerBySecondType in drawersFor.drawersByAttributeType.Values)
			{
				Debug.Assert(!propertyDrawerDrawerBySecondType.ContainsValue(null));
				Debug.Assert(!propertyDrawerDrawerBySecondType.ContainsValue(typeof(DecoratorDrawerDrawer)));
			}
			Debug.Assert(!drawersFor.propertyDrawerDrawersByFieldType.ContainsValue(null));
			Debug.Assert(!drawersFor.propertyDrawerDrawersByFieldType.ContainsValue(typeof(DecoratorDrawerDrawer)));

			Debug.Assert(!drawersFor.components.ContainsValue(null));
			Debug.Assert(!drawersFor.components.ContainsValue(typeof(CustomEditorAssetDrawer)));
			Debug.Assert(!drawersFor.components.ContainsValue(typeof(AssetDrawer)));

			Debug.Assert(!drawersFor.assets.ContainsValue(null));
			Debug.Assert(!drawersFor.assets.ContainsValue(typeof(CustomEditorComponentDrawer)));
			Debug.Assert(!drawersFor.assets.ContainsValue(typeof(ComponentDrawer)));
			Debug.Assert(!drawersFor.assetsByExtension.ContainsValue(typeof(CustomEditorComponentDrawer)));
			Debug.Assert(!drawersFor.assetsByExtension.ContainsValue(typeof(ComponentDrawer)));
			#endif
		}

		#if DEV_MODE && PI_ASSERTATIONS
		private bool AssertAllGUIInstructionTypesImplement([NotNull]IEnumerable<Type> drawerTypes, [NotNull]Type mustImplementInterface, [NotNull]DrawerFromPluginProvider provider, string methodName)
		{
			foreach(var drawerType in drawerTypes)
			{
				if(!mustImplementInterface.IsAssignableFrom(drawerType))
				{
					#if DEV_MODE
					Debug.LogError("Drawer Type "+drawerType.Name+" did not implement "+mustImplementInterface.Name+". "+provider.GetType().Name+"."+methodName+" has issues!");
					#endif
					return false;
				}
			}
			return true;
		}

		private bool AssertAllGUIInstructionTypesImplementOne(IEnumerable<Type> drawerTypes, Type mustImplementInterface1, Type mustImplementInterface2, [NotNull]DrawerFromPluginProvider provider, string methodName)
		{
			foreach(var drawerType in drawerTypes)
			{
				if(!mustImplementInterface1.IsAssignableFrom(drawerType) && !mustImplementInterface2.IsAssignableFrom(drawerType))
				{
					#if DEV_MODE
					Debug.LogError("Drawer Type "+drawerType.Name+" did not implement "+mustImplementInterface1.Name+" or "+mustImplementInterface2.Name+". "+provider.GetType().Name+"."+methodName+" has issues!");
					#endif
					return false;
				}
			}
			return true;
		}
		#endif
		
		private void BuildDictionariesForExactTypes(DrawerProviderData drawersFor, List<Type> drawerTypes, bool isFallback)
		{
			var fields = drawersFor.fields;
			var assetDrawers = drawersFor.assets;
			var components = drawersFor.components;
			var decoratorDrawers = drawersFor.decoratorDrawers;
			var drawersByAttributeType = drawersFor.drawersByAttributeType;
			var assetsByExtension = drawersFor.assetsByExtension;

			for(int n = drawerTypes.Count - 1; n >= 0; n--)
			{
				var drawerType = drawerTypes[n];

				#if DEV_MODE && PI_ASSERTATIONS
				Debug.Assert(!drawerType.IsAbstract);
				#endif

				var attributes = (DrawerForBaseAttribute[])drawerType.GetCustomAttributes(typeof(DrawerForBaseAttribute), false);
				for(int a = attributes.Length - 1; a >= 0; a--)
				{
					var attribute = attributes[a];

					#if DEV_MODE && PI_ASSERTATIONS
					attribute.AssertDataIsValid(drawerType);
					#endif

					if(attribute.isFallback != isFallback)
					{
						continue;
					}

					var type = attribute.Target;

					if(type != null)
					{
						#if DEV_MODE && DEBUG_BUILD_DICTIONARIES_FOR_NONFALLBACK_DRAWER
						if(!isFallback) { Debug.Log(StringUtils.ToStringSansNamespace(type)+" handled by "+StringUtils.ToStringSansNamespace(drawerType)); }
						#endif

						var fieldAttribute = attribute as DrawerForFieldAttribute;
						if(fieldAttribute != null)
						{
							if(!fields.ContainsKey(type))
							{
								#if DEV_MODE && DEBUG_BUILD_DICTIONARIES_FOR_FIELDS
								Debug.Log(StringUtils.ToStringSansNamespace(type)+" handled by "+StringUtils.ToStringSansNamespace(drawerType));
								#endif
								
								// NEW
								if(typeof(IPropertyDrawerDrawer).IsAssignableFrom(drawerType) && CustomEditorUtility.HasPropertyDrawer(type))
								{
									#if DEV_MODE
									Debug.Log("propertyDrawerDrawersByFieldType["+type.Name+"] = "+drawerType.Name);
									#endif
									drawersFor.propertyDrawerDrawersByFieldType.Add(type, drawerType);
									continue;
								}

								#if DEV_MODE && PI_ASSERTATIONS
								Debug.Assert(!drawersFor.propertyDrawerDrawersRequiringSerializedProperty.Contains(drawerType), drawerType);
								#endif

								fields.Add(type, drawerType);
							}
							continue;
						}
						
						var assetAttribute = attribute as DrawerForAssetAttribute;
						if(assetAttribute != null)
						{
							if(!assetDrawers.ContainsKey(type))
							{
								#if DEV_MODE && DEBUG_BUILD_DICTIONARIES_FOR_ASSETS
								Debug.Log(StringUtils.ToStringSansNamespace(type)+" handled by " + StringUtils.ToStringSansNamespace(drawerType));
								#endif

								if(type == Types.UnityObject && assetAttribute.TargetExtendingTypes && assetAttribute.isFallback)
								{
									if(typeof(ICustomEditorAssetDrawer).IsAssignableFrom(drawerType))
									{
										#if DEV_MODE
										Debug.Log("customEditorAssetDefault = " + drawerType.Name);
										#endif
										drawersFor.customEditorAssetDefault = drawerType;
									}
									else
									{
										#if DEV_MODE
										Debug.Log("assetDefault = " + drawerType.Name);
										#endif
										drawersFor.assetDefault = drawerType;
									}
								}
								else
								{
									assetDrawers.Add(type, drawerType);
								}
							}
							continue;
						}

						var componentAttribute = attribute as DrawerForComponentAttribute;
						if(componentAttribute != null)
						{
							if(!components.ContainsKey(type))
							{
								#if DEV_MODE && DEBUG_BUILD_DICTIONARIES_FOR_COMPONENTS
								Debug.Log(StringUtils.ToStringSansNamespace(type)+" handled by "+StringUtils.ToStringSansNamespace(drawerType));
								#endif

								if((type == Types.UnityObject || type == Types.Component) && componentAttribute.TargetExtendingTypes && componentAttribute.isFallback)
								{
									if(typeof(ICustomEditorComponentDrawer).IsAssignableFrom(drawerType))
									{
										#if DEV_MODE
										Debug.Log("customEditorComponentDefault = "+drawerType.Name);
										#endif
										drawersFor.customEditorComponentDefault = drawerType;
									}
									else
									{
										#if DEV_MODE
										Debug.Log("componentDefault = " + drawerType.Name);
										#endif
										drawersFor.componentDefault = drawerType;
									}
								}
								else
								{
									components.Add(type, drawerType);
								}
							}
							continue;
						}

						var decoratorDrawerAttribute = attribute as DrawerForDecoratorAttribute;
						if(decoratorDrawerAttribute != null)
						{
							foreach(var attributeType in PluginAttributeConverterProvider.GetAttributeTypeAndEachAlias(type))
							{
								if(!decoratorDrawers.ContainsKey(attributeType))
								{
									#if DEV_MODE && DEBUG_BUILD_DICTIONARIES_FOR_DECORATOR_DRAWERS
									Debug.Log(attributeType.FullName + " handled by " + drawerType.Name);
									#endif

									decoratorDrawers.Add(attributeType, drawerType);
								}
							}
							continue;
						}

						var propertyDrawerAttribute = attribute as DrawerForAttributeAttribute;
						if(propertyDrawerAttribute != null)
						{
							foreach(var attributeType in PluginAttributeConverterProvider.GetAttributeTypeAndEachAlias(type))
							{
								Dictionary<Type, Type> drawerTypesByFieldType;
								if(!drawersByAttributeType.TryGetValue(attributeType, out drawerTypesByFieldType))
								{
									drawerTypesByFieldType = new Dictionary<Type, Type>(1);
									drawersByAttributeType.Add(attributeType, drawerTypesByFieldType);
								}

								var fieldType = propertyDrawerAttribute.valueType;
								if(!drawerTypesByFieldType.ContainsKey(fieldType))
								{
									drawerTypesByFieldType.Add(fieldType, drawerType);

									#if DEV_MODE && DEBUG_BUILD_DICTIONARIES_FOR_PROPERTY_DRAWERS
									Debug.Log(attributeType.FullName + " handled by "+drawerType.Name+" with field type " + fieldType.Name);
									#endif

									#if DEV_MODE && PI_ASSERTATIONS
									Debug.Assert(drawerTypesByFieldType[fieldType] == drawerType);
									#endif
								}

								#if DEV_MODE && PI_ASSERTATIONS
								Debug.Assert(drawersByAttributeType.ContainsKey(attributeType));
								Debug.Assert(drawerTypesByFieldType.ContainsKey(fieldType));
								#endif
							}
							continue;
						}

						var gameObjectDrawerAttribute = attribute as DrawerForGameObjectAttribute;
						if(gameObjectDrawerAttribute != null)
						{
							if(gameObjectDrawerAttribute.requireComponentOnGameObject == null)
							{
								if(gameObjectDrawerAttribute.isCategorizedGameObjectDrawer)
								{
									if(drawersFor.gameObjectCategorized == null || !gameObjectDrawerAttribute.isFallback)
									{
										drawersFor.gameObjectCategorized = drawerType;
									}
								}
								else if(drawersFor.gameObject == null || !gameObjectDrawerAttribute.isFallback)
								{
									drawersFor.gameObject = drawerType;
								}
							}
							else
							{
								drawersFor.gameObjectByComponent[gameObjectDrawerAttribute.requireComponentOnGameObject] = drawerType;
							}
						}
						#if DEV_MODE
						else { Debug.LogError("Unrecognized DrawerForBaseAttribute type: "+attribute.GetType().Name); }
						#endif
					}
					else
					{
						var extensionAttribute = attribute as DrawerByExtensionAttribute;
						if(extensionAttribute != null)
						{
							var extension = extensionAttribute.fileExtension;

							#if DEV_MODE && PI_ASSERTATIONS
							Debug.Assert(!string.IsNullOrEmpty(extension), "fileExtension was null or empty on DrawerByExtension attribute of " + StringUtils.TypeToString(drawerType));
							#endif
							
							if(!assetsByExtension.ContainsKey(extension))
							{
								#if DEV_MODE && DEBUG_BUILD_DICTIONARIES_FOR_ASSETS
								Debug.Log("Asset extension \""+extension+"\" handled by "+StringUtils.ToStringSansNamespace(drawerType));
								#endif

								assetsByExtension.Add(extension, drawerType);
							}

							#if DEV_MODE && PI_ASSERTATIONS
							Debug.Assert(assetsByExtension.ContainsKey(extension));
							#endif
						}
						#if DEV_MODE
						else { Debug.LogWarning("Ignoring Attribute "+attribute.GetType().Name+" because it had a null Type"); }
						#endif
					}
				}
			}
		}

		private void BuildDictionariesForInheritedTypes(DrawerProviderData drawerProviderData, List<Type> drawerTypes, bool isFallback)
		{
			var fields = drawerProviderData.fields;
			var assetDrawers = drawerProviderData.assets;
			var components = drawerProviderData.components;
			var decoratorDrawers = drawerProviderData.decoratorDrawers;
			var propertyDrawersByAttributeType = drawerProviderData.drawersByAttributeType;

			for(int n = drawerTypes.Count - 1; n >= 0; n--)
			{
				var drawerType = drawerTypes[n];
				var attributes = (DrawerForBaseAttribute[])drawerType.GetCustomAttributes(typeof(DrawerForBaseAttribute), false);
				for(int a = attributes.Length - 1; a >= 0; a--)
				{
					var attribute = attributes[a];

					if(attribute.isFallback != isFallback)
					{
						continue;
					}

					if(!attribute.TargetExtendingTypes)
					{
						continue;
					}

					var type = attribute.Target;

					// if has no target type or target type is a value type
					// then there are no inhertied types that need to be added
					// for these drawers
					if(type == null || type.IsValueType)
					{
						continue;
					}
					
					#if DEV_MODE && DEBUG_BUILD_DICTIONARIES_FOR_NONFALLBACK_DRAWER
					if(!isFallback) { Debug.Log(StringUtils.ToStringSansNamespace(type)+" inherited types handled by "+StringUtils.ToStringSansNamespace(drawerType)); }
					#endif

					var fieldAttribute = attribute as DrawerForFieldAttribute;
					if(fieldAttribute != null)
					{
						Type[] subjectTypes;
						if(type == Types.Enum)
						{
							subjectTypes = TypeExtensions.EnumTypesIncludingInvisible;
						}
						else if(type.IsInterface)
						{
							subjectTypes = type.GetImplementingTypes(true);
						}
						else
						{
							subjectTypes = type.GetExtendingTypes(true);
						}

						for(int t = subjectTypes.Length - 1; t >= 0; t--)
						{
							var subjectType = subjectTypes[t];

							// NEW
							if(typeof(IPropertyDrawerDrawer).IsAssignableFrom(drawerType) && CustomEditorUtility.HasPropertyDrawer(subjectType))
							{
								#if DEV_MODE
								Debug.Log("propertyDrawerDrawersByFieldType["+ subjectType.Name+"] = "+drawerType.Name);
								#endif
								drawersFor.propertyDrawerDrawersByFieldType.Add(subjectType, drawerType);
								continue;
							}

							if(!fields.ContainsKey(subjectType))
							{
								#if DEV_MODE && DEBUG_BUILD_DICTIONARIES_FOR_ENUM_FIELDS
								if(drawerType == typeof(EnumDrawer))
								{
									Debug.Log(subjectType.FullName + " handled by "+drawerType.Name+" because it's a base type of "+type.FullName);
								}
								#endif
								#if DEV_MODE && DEBUG_BUILD_DICTIONARIES_FOR_DELEGATE_FIELDS
								if(drawerType == typeof(DelegateDrawer))
								{
									Debug.Log(subjectType.FullName + " handled by "+drawerType.Name+" because it's a base type of "+type.FullName);
								}
								#endif
								#if DEV_MODE && DEBUG_BUILD_DICTIONARIES_FOR_UNITY_OBJECT_FIELDS
								if(drawerType == typeof(ObjectReferenceDrawer))
								{
									Debug.Log(subjectType.FullName + " handled by "+drawerType.Name+" because it's a base type of "+type.FullName);
								}
								#endif
								#if DEV_MODE && DEBUG_BUILD_DICTIONARIES_FOR_FIELDS
								if(drawerType != typeof(EnumDrawer) && drawerType != typeof(DelegateDrawer) && drawerType != typeof(ObjectReferenceDrawer))
								{
									Debug.Log(StringUtils.ToStringSansNamespace(subjectType) + " handled by "+StringUtils.ToStringSansNamespace(drawerType)+" because it's a base type of "+StringUtils.ToStringSansNamespace(type));
								}
								#endif

								#if DEV_MODE && PI_ASSERTATIONS
								Debug.Assert(!drawersFor.propertyDrawerDrawersRequiringSerializedProperty.Contains(drawerType));
								#endif

								fields.Add(subjectType, drawerType);
							}
						}
						continue;
					}

					var decoratorDrawerAttribute = attribute as DrawerForDecoratorAttribute;
					if(decoratorDrawerAttribute != null)
					{
						foreach(var attributeType in PluginAttributeConverterProvider.GetAttributeTypeAndEachAlias(type))
						{
							var subjectTypes = attributeType.GetExtendingNonUnityObjectClassTypes(false);
							for(int t = subjectTypes.Length - 1; t >= 0; t--)
							{
								var subjectType = subjectTypes[t];

								if(!decoratorDrawers.ContainsKey(subjectType))
								{
									#if DEV_MODE && DEBUG_BUILD_DICTIONARIES_FOR_DECORATOR_DRAWERS
									Debug.Log(subjectType.FullName + " handled by " + drawerType.Name + " because it's a base type of " + attributeType.FullName);
									#endif

									decoratorDrawers.Add(subjectType, drawerType);
								}
							}
						}
						continue;
					}

					var propertyDrawerAttribute = attribute as DrawerForAttributeAttribute;
					if(propertyDrawerAttribute != null)
					{
						foreach(var attributeType in PluginAttributeConverterProvider.GetAttributeTypeAndEachAlias(type))
						{
							var subjectTypes = attributeType.GetExtendingNonUnityObjectClassTypes(false);
							for(int t = subjectTypes.Length - 1; t >= 0; t--)
							{
								var subjectType = subjectTypes[t];
								Dictionary<Type, Type> drawerTypesByFieldType;
								if(!propertyDrawersByAttributeType.TryGetValue(subjectType, out drawerTypesByFieldType))
								{
									drawerTypesByFieldType = new Dictionary<Type, Type>(1);
									propertyDrawersByAttributeType.Add(subjectType, drawerTypesByFieldType);
								}

								var fieldType = propertyDrawerAttribute.valueType;
								if(!drawerTypesByFieldType.ContainsKey(fieldType))
								{
									drawerTypesByFieldType.Add(fieldType, drawerType);

									#if DEV_MODE && DEBUG_BUILD_DICTIONARIES_FOR_PROPERTY_DRAWERS
									Debug.Log(subjectType.Name+" handled by "+drawerType.Name+" with field type "+fieldType.Name+" because it's a base type of "+attributeType.FullName);
									#endif

									#if DEV_MODE && PI_ASSERTATIONS
									Debug.Assert(drawerTypesByFieldType[fieldType] == drawerType);
									#endif
								}

								#if DEV_MODE && PI_ASSERTATIONS
								Debug.Assert(propertyDrawersByAttributeType.ContainsKey(subjectType));
								Debug.Assert(drawerTypesByFieldType.ContainsKey(fieldType));
								#endif
							}
						}
						continue;
					}

					var componentAttribute = attribute as DrawerForComponentAttribute;
					if(componentAttribute != null)
					{
						// UPDATE: This is now handled via componentDefault and customEditorComponentDefault instead
						if((type == Types.UnityObject || type == Types.Component) && componentAttribute.TargetExtendingTypes && componentAttribute.isFallback)
						{
							continue;
						}
						
						var subjectTypes = type.IsInterface ? type.GetImplementingComponentTypes(true) : type.GetExtendingComponentTypes(true);
						for(int t = subjectTypes.Length - 1; t >= 0; t--)
						{
							var subjectType = subjectTypes[t];

							if(subjectType.IsAbstract)
							{
								#if DEV_MODE && DEBUG_BUILD_DICTIONARIES_FOR_COMPONENTS
								Debug.Log("Skipping component "+ subjectType + " because it's abstract...");
								#endif
								continue;
							}

							#if DEV_MODE && PI_ASSERTATIONS
							Debug.Assert(subjectType.IsComponent(), "DrawerForComponent attribute subject has to be Component type: " + StringUtils.ToString(type));
							#endif

							if(!components.ContainsKey(subjectType))
							{
								components.Add(subjectType, drawerType);
							}
						}
						continue;
					}

					var assetAttribute = attribute as DrawerForAssetAttribute;
					if(assetAttribute != null)
					{
						// UPDATE: This is now handled via assetDefault and customEditorAssetDefault instead
						if(type == Types.UnityObject && assetAttribute.TargetExtendingTypes && assetAttribute.isFallback)
						{
							continue;
						}

						var subjectTypes = type.IsInterface ? type.GetImplementingUnityObjectTypes(true) : type.GetExtendingUnityObjectTypes(true);
						for(int t = subjectTypes.Length - 1; t >= 0; t--)
						{
							var subjectType = subjectTypes[t];

							if(subjectType.IsAbstract)
							{
								#if DEV_MODE && DEBUG_BUILD_DICTIONARIES_FOR_ASSETS
								Debug.Log("Skipping inherited asset type "+ subjectType + " because it's abstract...");
								#endif
								continue;
							}

							if(subjectType.IsComponent())
							{
								#if DEV_MODE && PI_ASSERTATIONS
								Debug.Assert(!type.IsInterface, "DrawerForAsset attribute subject can not be Component type: "+StringUtils.ToString(type));
								#endif
								continue;
							}
							
							if(!assetDrawers.ContainsKey(subjectType))
							{
								#if DEV_MODE && DEBUG_BUILD_DICTIONARIES_FOR_ASSETS
								Debug.Log(StringUtils.ToStringSansNamespace(subjectType)+" handled by "+StringUtils.ToStringSansNamespace(drawerType));
								#endif

								assetDrawers.Add(subjectType, drawerType);
							}
						}
					}
				}
			}
		}

		#if UNITY_EDITOR
		// Make all Custom Editors existing in namespaces other than Unity's use CustomEditorComponentDrawer or CustomEditorAssetDrawer
		private void BuildDictionariesForCustomEditorsInNonUnityNamespaces(DrawerProviderData drawersFor, Dictionary<Type, Type> customEditorsByType)
		{
			var componentDrawers = drawersFor.components;
			var assetDrawers = drawersFor.assets;

			foreach(var customEditor in customEditorsByType)
			{
				var editorType = customEditor.Value;
				if(!IsInUnityNamespace(editorType))
				{
					AssignTypeToUseCustomEditorDrawer(componentDrawers, assetDrawers, customEditor.Key);
				}
			}
		}

		// Make all Custom Editors existing in Unity namespaces use CustomEditorComponentDrawer or CustomEditorAssetDrawer
		private void BuildDictionariesForCustomEditorsInUnityNamespaces(DrawerProviderData drawersFor, Dictionary<Type, Type> customEditorsByType)
		{
			var componentDrawers = drawersFor.components;
			var assetDrawers = drawersFor.assets;

			foreach(var customEditor in customEditorsByType)
			{
				var editorType = customEditor.Value;
				if(IsInUnityNamespace(editorType))
				{
					AssignTypeToUseCustomEditorDrawer(componentDrawers, assetDrawers, customEditor.Key);
				}
			}
		}

		private void AssignTypeToUseCustomEditorDrawer(Dictionary<Type, Type> componentDrawers, Dictionary<Type, Type> assetDrawers, Type targetType)
		{
			if(targetType.IsComponent())
			{
				if(!componentDrawers.ContainsKey(targetType))
				{
					#if DEV_MODE && (DEBUG_BUILD_DICTIONARIES_FOR_COMPONENTS || DEBUG_BUILD_DICTIONARIES_FOR_CUSTOM_EDITORS)
					Debug.Log(targetType.Name+" handled by CustomEditorComponentDrawer");
					#endif

					componentDrawers.Add(targetType, typeof(CustomEditorComponentDrawer));
				}
				#if DEV_MODE && (DEBUG_BUILD_DICTIONARIES_FOR_COMPONENTS || DEBUG_BUILD_DICTIONARIES_FOR_CUSTOM_EDITORS)
				else { Debug.Log("Won't use CustomEditorComponentDrawer for "+targetType.Name+" because already handled by "+componentDrawers[targetType].Name); }
				#endif
			}
			else
			{
				if(!assetDrawers.ContainsKey(targetType))
				{
					if(Types.TextAsset.IsAssignableFrom(targetType))
					{
						#if DEV_MODE && (DEBUG_BUILD_DICTIONARIES_FOR_ASSETS || DEBUG_BUILD_DICTIONARIES_FOR_CUSTOM_EDITORS)
						Debug.Log(targetType.Name+" handled by CustomEditorTextAssetDrawer");
						#endif

						assetDrawers.Add(targetType, typeof(CustomEditorTextAssetDrawer));
					}
					else
					{
						#if DEV_MODE && (DEBUG_BUILD_DICTIONARIES_FOR_ASSETS || DEBUG_BUILD_DICTIONARIES_FOR_CUSTOM_EDITORS)
						Debug.Log(targetType.Name+" handled by CustomEditorAssetDrawer");
						#endif

						assetDrawers.Add(targetType, typeof(CustomEditorAssetDrawer));
					}
				}
				#if DEV_MODE && (DEBUG_BUILD_DICTIONARIES_FOR_ASSETS || DEBUG_BUILD_DICTIONARIES_FOR_CUSTOM_EDITORS)
				else { Debug.Log("Won't use CustomEditorAssetDrawer for "+targetType.Name+" because already handled by "+assetDrawers[targetType].Name); }
				#endif
			}
		}

		// Make all PropertyDrawer backed fields existing in namespaces other than Unity's use PropertyDrawerDrawer
		private void BuildDictionariesForPropertyDrawersInNonUnityNamespaces(DrawerProviderData drawersFor, Dictionary<Type, Type> propertyDrawers)
		{
			foreach(var propertyDrawer in propertyDrawers)
			{
				var attributeOrFieldType = propertyDrawer.Key;

				if(!IsInUnityNamespace(attributeOrFieldType))
				{
					AssignTypeToUsePropertyDrawerDrawer(drawersFor, attributeOrFieldType);
				}
			}
		}

		// Make PropertyDrawer backed type use to PropertyDrawerDrawer
		private void BuildDictionariesForPropertyDrawersInUnityNamespaces(DrawerProviderData drawersFor, Dictionary<Type, Type> propertyDrawers)
		{
			foreach(var propertyDrawer in propertyDrawers)
			{
				var attributeOrFieldType = propertyDrawer.Key;
				if(IsInUnityNamespace(attributeOrFieldType))
				{
					AssignTypeToUsePropertyDrawerDrawer(drawersFor, attributeOrFieldType);
				}
			}
		}

		private void AssignTypeToUsePropertyDrawerDrawer(DrawerProviderData drawersFor, Type attributeOrFieldType)
		{
			#if DEV_MODE && DEBUG_BUILD_DICTIONARIES_FOR_PROPERTY_DRAWERS
			Debug.Log("AssignTypeToUsePropertyDrawerDrawer("+ StringUtils.ToString(attributeOrFieldType)+")...");
			#endif

			if(attributeOrFieldType.IsSubclassOf(Types.PropertyAttribute))
			{
				Dictionary<Type, Type> drawerTypesByFieldType;
				if(drawersFor.drawersByAttributeType.TryGetValue(attributeOrFieldType, out drawerTypesByFieldType))
				{
					if(drawerTypesByFieldType.ContainsKey(Types.SystemObject))
					{
						#if DEV_MODE && DEBUG_BUILD_DICTIONARIES_FOR_PROPERTY_DRAWERS
						Debug.LogWarning("AssignTypeToUsePropertyDrawerDrawer("+ StringUtils.ToString(attributeOrFieldType)+ ") - won't add because type already using "+ StringUtils.ToString(drawerTypesByFieldType[attributeOrFieldType]));
						#endif
						return;
					}
				}
				else
				{
					drawerTypesByFieldType = new Dictionary<Type, Type>(1);
					drawersFor.drawersByAttributeType.Add(attributeOrFieldType, drawerTypesByFieldType);
				}
				
				#if DEV_MODE && DEBUG_BUILD_DICTIONARIES_FOR_PROPERTY_DRAWERS
				Debug.Log(attributeOrFieldType.Name+" (PropertyDrawer) default field type System.Object handled by PropertyDrawerDrawer.");
				#endif
				
				// all PropertyDrawers backed fields default to PropertyDrawerDrawer
				drawerTypesByFieldType.Add(Types.SystemObject, typeof(PropertyDrawerDrawer));
			}
			else if(!drawersFor.propertyDrawerDrawersByFieldType.ContainsKey(attributeOrFieldType))
			{
				#if DEV_MODE && DEBUG_BUILD_DICTIONARIES_FOR_PROPERTY_DRAWERS
				Debug.Log("AssignTypeToUsePropertyDrawerDrawer("+ StringUtils.ToString(attributeOrFieldType)+ "): adding using PropertyDrawerDrawer");
				#endif

				// NEW: If there already is a non-fallback drawer specified using DrawerForFieldAttribute for the attribute / field, then prefer that over the default PropertyDrawerDrawer.
				// This makes it possible to create drawers that replace / enhance property drawers included in the project.
				Type customDrawerType;
				if(drawersFor.fields.TryGetValue(attributeOrFieldType, out customDrawerType))
				{
					#if DEV_MODE && PI_ASSERTATIONS
					Debug.Assert(customDrawerType != null);
					#endif

					var drawerForFieldAttributes = customDrawerType.GetCustomAttributes(typeof(DrawerForFieldAttribute), false);
					if(drawerForFieldAttributes.Length > 0 && !(drawerForFieldAttributes[0] as DrawerForFieldAttribute).isFallback)
					{
						#if DEV_MODE && DEBUG_BUILD_DICTIONARIES_FOR_PROPERTY_DRAWERS
						Debug.LogWarning("Won't use PropertyDrawer for " + attributeOrFieldType.Name + " because non-fallback drawer " + customDrawerType.Name + " is already used for the field.");
						#endif
						return;
					}
				}

				// all PropertyDrawers backed fields default to PropertyDrawerDrawer
				drawersFor.propertyDrawerDrawersByFieldType.Add(attributeOrFieldType, typeof(PropertyDrawerDrawer));
			}
			#if DEV_MODE && DEBUG_BUILD_DICTIONARIES_FOR_PROPERTY_DRAWERS
			else { Debug.Log("AssignTypeToUsePropertyDrawerDrawer("+ StringUtils.ToString(attributeOrFieldType)+ ") - won't add because type already using "+ StringUtils.ToString(drawersFor.propertyDrawerDrawersByFieldType[attributeOrFieldType])); }
			#endif
		}

		// Make all PropertyDrawer backed fields existing in namespaces other than Unity's use PropertyDrawerDrawer
		private void BuildDictionariesForDecoratorDrawersInNonUnityNamespaces(DrawerProviderData drawersFor, Dictionary<Type, Type> decoratorDrawers)
		{
			foreach(var decoratorDrawer in decoratorDrawers)
			{
				#if DEV_MODE && PI_ASSERTATIONS
				Debug.Assert(typeof(DecoratorDrawer).IsAssignableFrom(decoratorDrawer.Value));
				#endif

				var attributeOrFieldType = decoratorDrawer.Key;

				if(!IsInUnityNamespace(attributeOrFieldType))
				{
					if(DecoratorDrawerDrawerBlacklist.Contains(attributeOrFieldType))
					{
						continue;
					}

					AssignTypeToUseDecoratorDrawerDrawer(drawersFor, attributeOrFieldType);
				}
			}
		}

		// Make PropertyDrawer backed type use to PropertyDrawerDrawer
		private void BuildDictionariesForDecoratorDrawersInUnityNamespaces(DrawerProviderData drawersFor, Dictionary<Type, Type> decoratorDrawers)
		{
			foreach(var decoratorDrawer in decoratorDrawers)
			{
				#if DEV_MODE && PI_ASSERTATIONS
				Debug.Assert(typeof(DecoratorDrawer).IsAssignableFrom(decoratorDrawer.Value));
				#endif

				var attributeOrFieldType = decoratorDrawer.Key;

				if(IsInUnityNamespace(attributeOrFieldType))
				{
					AssignTypeToUseDecoratorDrawerDrawer(drawersFor, attributeOrFieldType);
				}
			}
		}

		private void AssignTypeToUseDecoratorDrawerDrawer(DrawerProviderData drawersFor, Type attributeType)
		{
			if(drawersFor.decoratorDrawers.ContainsKey(attributeType))
			{
				return;
			}

			#if DEV_MODE && DEBUG_BUILD_DICTIONARIES_FOR_DECORATOR_DRAWERS
			Debug.Log(attributeType.Name+ " (DecoratorDrawer) with default field type System.Object handled by DecoratorDrawerDrawer.");
			#endif

			drawersFor.decoratorDrawers.Add(attributeType, typeof(DecoratorDrawerDrawer));
		}
		
		private bool IsInUnityNamespace(Type type)
		{
			return TypeExtensions.IsInUnityNamespaceThreadSafe(type);
		}
		#endif
	}
}