#define SUPPORT_EDITORS_FOR_INTERFACES // the default inspector doesn't support this but we can

//#define DEBUG_CUSTOM_EDITORS
//#define DEBUG_PROPERTY_DRAWERS
//#define DEBUG_SET_EDITING_TEXT_FIELD

#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using Sisus.Compatibility;

#if DEV_MODE && DEBUG_CUSTOM_EDITORS
using System.Linq;
#endif

namespace Sisus
{
	public static class CustomEditorUtility
	{
		private static Dictionary<Type, Type> customEditorsByType;
		private static Dictionary<Type, Type> propertyDrawersByType;
		private static Dictionary<Type, Type> decoratorDrawersByType;

		public static Dictionary<Type, Type> CustomEditorsByType
		{
			get
			{
				if(customEditorsByType == null)
				{
					customEditorsByType = new Dictionary<Type, Type>();

					var inspectedTypeField = typeof(CustomEditor).GetField("m_InspectedType", BindingFlags.NonPublic | BindingFlags.Instance);
					var useForChildrenField = typeof(CustomEditor).GetField("m_EditorForChildClasses", BindingFlags.NonPublic | BindingFlags.Instance);

					IEnumerable<Type> editorTypes;
					var ignored = PluginAttributeConverterProvider.ignoredEditors;
					//NOTE: important to also get invisible types, so that internal Editors such as RectTransformEditor are also returned
					if(ignored == null || ignored.Count == 0)
					{
						editorTypes = TypeExtensions.GetAllTypesThreadSafe(false, true, true).Where((t) => t.IsSubclassOf(Types.Editor));
					}
					else
					{
						editorTypes = TypeExtensions.GetAllTypesThreadSafe(false, true, true).Where((t) => t.IsSubclassOf(Types.Editor) && !ignored.Contains(t));
					}

					#if DEV_MODE
					if(!editorTypes.Contains(Types.GetInternalEditorType("UnityEditor.RectTransformEditor"))) { Debug.LogError("RectTransformEditor was not among "+ editorTypes.Count()+" Editors!"); };
					#endif

					GetDrawersByInspectedTypeFromAttributes<CustomEditor>(editorTypes, inspectedTypeField, ref customEditorsByType, false);
					
					//second pass: also apply for inheriting types if they don't already have more specific overrides
					GetDrawersByInheritedInspectedTypesFromAttributes<CustomEditor>(editorTypes, inspectedTypeField, useForChildrenField, ref customEditorsByType, true, false);
					
					#if DEV_MODE && DEBUG_CUSTOM_EDITORS
					var log = customEditorsByType.Where(pair => !Types.Component.IsAssignableFrom(pair.Key));
					Debug.Log("Non-Components with custom editors:\n"+StringUtils.ToString(log, "\n"));
					#endif
				}

				return customEditorsByType;
			}
		}

		public static Dictionary<Type, Type> PropertyDrawersByType
		{
			get
			{
				if(propertyDrawersByType == null)
				{
					propertyDrawersByType = new Dictionary<Type, Type>();

					var typeField = typeof(CustomPropertyDrawer).GetField("m_Type", BindingFlags.NonPublic | BindingFlags.Instance);
					var useForChildrenField = typeof(CustomPropertyDrawer).GetField("m_UseForChildren", BindingFlags.NonPublic | BindingFlags.Instance);
					var propertyDrawerTypes = TypeExtensions.GetAllTypesThreadSafe(false, true, true).Where((type) => type.IsSubclassOf(typeof(UnityEditor.PropertyDrawer)));

					#if DEV_MODE && PI_ASSERTATIONS
					if(!propertyDrawerTypes.Contains(typeof(UnityEditorInternal.UnityEventDrawer))) { Debug.LogError("UnityEventDrawer not found among "+ propertyDrawerTypes.Count()+" PropertyDrawer types:\n"+StringUtils.ToString(propertyDrawerTypes, "\n")); };
					#endif

					GetDrawersByInspectedTypeFromAttributes<CustomPropertyDrawer>(propertyDrawerTypes, typeField, ref propertyDrawersByType, true);
					//second pass: also apply for inheriting types if they don't already have more specific overrides
					GetDrawersByInheritedInspectedTypesFromAttributes<CustomPropertyDrawer>(propertyDrawerTypes, typeField, useForChildrenField, ref propertyDrawersByType, false, true);

					#if DEV_MODE && PI_ASSERTATIONS
					if(!propertyDrawersByType.Values.Contains(typeof(UnityEditorInternal.UnityEventDrawer))) { Debug.LogError("UnityEventDrawer not found among "+ propertyDrawersByType.Count+" PropertyDrawers. "); };
					#endif

					#if DEV_MODE && DEBUG_PROPERTY_DRAWERS
					Debug.Log("propertyDrawersByType:\r\n"+StringUtils.ToString(propertyDrawersByType, "\r\n"));
					#endif
				}
				
				return propertyDrawersByType;
			}
		}

		public static Dictionary<Type, Type> DecoratorDrawersByType
		{
			get
			{
				if(decoratorDrawersByType == null)
				{
					decoratorDrawersByType = new Dictionary<Type, Type>();

					var typeField = typeof(CustomPropertyDrawer).GetField("m_Type", BindingFlags.NonPublic | BindingFlags.Instance);
					// UPDATE: Apparently this field is not really respected for decorator drawers
					//var useForChildrenField = propertyDrawerType.GetField("m_UseForChildren", BindingFlags.NonPublic | BindingFlags.Instance);

					var decoratorDrawerTypes = TypeExtensions.GetAllTypesThreadSafe(false, true, true).Where((type) => type.IsSubclassOf(typeof(DecoratorDrawer)));

					GetDrawersByInspectedTypeFromAttributes<CustomPropertyDrawer>(decoratorDrawerTypes, typeField, ref decoratorDrawersByType, false);
					GetDrawersByInheritedInspectedTypesFromAttributes<CustomPropertyDrawer>(decoratorDrawerTypes, typeField, null, ref decoratorDrawersByType, false, false);
				}
				
				return decoratorDrawersByType;
			}
		}
		
		/// <summary>
		/// Attempts to get PropertyDrawer Type for given class or from attributes on the field
		/// </summary>
		/// <param name="classMemberType"> Type of the class for which we are trying to find the PropertyDrawer. </param>
		/// <param name="memberInfo"> LinkedMemberInfo of the property for which we are trying to find the PropertyDrawer. </param>
		/// <param name="propertyAttribute"> [out] PropertyAttribute found on the property. </param>
		/// <param name="drawerType"> [out] Type of the PropertyDrawer for the PropertyAttribute. </param>
		/// <returns>
		/// True if target has a PropertyDrawer, false if not.
		/// </returns>
		public static bool TryGetPropertyDrawerType([NotNull]Type classMemberType, [NotNull]LinkedMemberInfo memberInfo, out PropertyAttribute propertyAttribute, out Type drawerType)
		{
			var attributes = memberInfo.GetAttributes(Types.PropertyAttribute);
			for(int n = attributes.Length - 1; n >= 0; n--)
			{
				var attribute = attributes[n];
				if(TryGetPropertyDrawerType(attribute.GetType(), out drawerType))
				{
					propertyAttribute = attribute as PropertyAttribute;
					return true;
				}
			}
			propertyAttribute = null;
			return TryGetPropertyDrawerType(classMemberType, out drawerType);
		}

		/// <summary>
		/// Attempts to get PropertyDrawer Type for given class.
		/// </summary>
		/// <param name="classMemberOrAttributeType"> Type of the class for which we are trying to find the PropertyDrawer. </param>
		/// <param name="propertyDrawerType"> [out] Type of the PropertyDrawer. </param>
		/// <returns> True if target has a PropertyDrawer, false if not. </returns>
		public static bool TryGetPropertyDrawerType([NotNull]Type classMemberOrAttributeType, out Type propertyDrawerType)
		{
			if(PropertyDrawersByType.TryGetValue(classMemberOrAttributeType, out propertyDrawerType))
			{
				return true;
			}

			if(classMemberOrAttributeType.IsGenericType && !classMemberOrAttributeType.IsGenericTypeDefinition)
			{
				return propertyDrawersByType.TryGetValue(classMemberOrAttributeType.GetGenericTypeDefinition(), out propertyDrawerType);
			}

			return false;
		}

		/// <summary>
		/// Does class member attribute of given type have a PropertyDrawer?
		/// </summary>
		/// <param name="classMemberOrAttributeType"> Type of class member or attribute. </param>
		/// <returns> True if class has a PropertyDrawer, false if not. </returns>
		public static bool HasPropertyDrawer([NotNull]Type classMemberOrAttributeType)
		{
			if(PropertyDrawersByType.ContainsKey(classMemberOrAttributeType))
			{
				return true;
			}

			if(classMemberOrAttributeType.IsGenericType && !classMemberOrAttributeType.IsGenericTypeDefinition)
			{
				return propertyDrawersByType.ContainsKey(classMemberOrAttributeType.GetGenericTypeDefinition());
			}

			return false;
		}

		public static bool TryGetDecoratorDrawerTypes([NotNull]LinkedMemberInfo memberInfo, out object[] decoratorAttributes, out Type[] drawerTypes)
		{
			//TO DO: Add support for PropertyAttribute.order
			
			drawerTypes = null;
			decoratorAttributes = null;
			
			var attributes = memberInfo.GetAttributes(Types.PropertyAttribute);
			for(int n = attributes.Length - 1; n >= 0; n--)
			{
				var attribute = attributes[n];
				Type drawerType;
				if(TryGetDecoratorDrawerType(attribute.GetType(), out drawerType))
				{
					if(drawerTypes == null)
					{
						decoratorAttributes = new[] { attribute };
						drawerTypes = new[]{drawerType};
					}
					else
					{
						decoratorAttributes = decoratorAttributes.Add(attribute);
						drawerTypes = drawerTypes.Add(drawerType);
					}
				}
			}
			
			return drawerTypes != null;
		}

		public static bool TryGetDecoratorDrawerTypes([NotNull]MemberInfo memberInfo, out object[] decoratorAttributes, out Type[] drawerTypes)
		{
			//TO DO: Add support for PropertyAttribute.order
			// 
			drawerTypes = null;
			decoratorAttributes = null;
			
			var attributes = memberInfo.GetCustomAttributes(Types.PropertyAttribute, true);
			for(int n = attributes.Length - 1; n >= 0; n--)
			{
				var attribute = attributes[n];
				Type drawerType;
				if(TryGetDecoratorDrawerType(attribute.GetType(), out drawerType))
				{
					if(drawerTypes == null)
					{
						decoratorAttributes = new[] { attribute };
						drawerTypes = new[]{drawerType};
					}
					else
					{
						decoratorAttributes = decoratorAttributes.Add(attribute);
						drawerTypes = drawerTypes.Add(drawerType);
					}
				}
			}
			
			return drawerTypes != null;
		}
		
		public static bool AttributeHasDecoratorDrawer(Type propertyAttributeType)
		{
			if(DecoratorDrawersByType.ContainsKey(propertyAttributeType))
			{
				return true;
			}

			if(propertyAttributeType.IsGenericType && !propertyAttributeType.IsGenericTypeDefinition)
			{
				return decoratorDrawersByType.ContainsKey(propertyAttributeType.GetGenericTypeDefinition());
			}

			return false;
		}

		public static bool TryGetDecoratorDrawerType(Type propertyAttributeType, out Type decoratorDrawerType)
		{
			if(DecoratorDrawersByType.TryGetValue(propertyAttributeType, out decoratorDrawerType))
			{
				return true;
			}

			if(propertyAttributeType.IsGenericType && !propertyAttributeType.IsGenericTypeDefinition)
			{
				return decoratorDrawersByType.TryGetValue(propertyAttributeType.GetGenericTypeDefinition(), out decoratorDrawerType);
			}

			return false;
		}
		
		public static bool TryGetCustomEditorType(Type targetType, out Type editorType)
		{
			if(CustomEditorsByType.TryGetValue(targetType, out editorType))
			{
				return true;
			}

			if(targetType.IsGenericType && !targetType.IsGenericTypeDefinition)
			{
				return customEditorsByType.TryGetValue(targetType.GetGenericTypeDefinition(), out editorType);
			}

			return false;
		}

		/// <summary>
		/// Given an array of PropertyDrawer, DecoratorDrawers or Editors, gets their inspected types and adds them to drawersByInspectedType.
		/// </summary>
		/// <typeparam name="TAttribute"> Type of the attribute. </typeparam>
		/// <param name="drawerOrEditorTypes"> List of PropertyDrawer, DecoratorDrawer or Editor types. </param>
		/// <param name="targetTypeField"> FieldInfo for getting the inspected type. </param>
		/// <param name="drawersByInspectedType">
		/// [in,out] dictionary where drawer types will be added with their inspected types as the keys. </param>
		private static void GetDrawersByInspectedTypeFromAttributes<TAttribute>([NotNull]IEnumerable<Type> drawerOrEditorTypes, [NotNull]FieldInfo targetTypeField, [NotNull]ref Dictionary<Type,Type> drawersByInspectedType, bool canBeAbstract) where TAttribute : Attribute
		{
			var attType = typeof(TAttribute);
			
			foreach(var drawerOrEditorType in drawerOrEditorTypes)
			{
				#if DEV_MODE
				Debug.Assert(!drawerOrEditorType.IsAbstract);
				#endif

				//if(!drawerOrEditorType.IsAbstract)
				{
					var attributes = drawerOrEditorType.GetCustomAttributes(attType, true);
					for(int a = attributes.Length - 1; a >= 0; a--)
					{
						var attribute = attributes[a];
						var inspectedType = targetTypeField.GetValue(attribute) as Type;
						if(!inspectedType.IsAbstract || canBeAbstract)
						{
							drawersByInspectedType[inspectedType] = drawerOrEditorType;
						}
					}
				}
			}
		}

		private static void GetDrawersByInheritedInspectedTypesFromAttributes<TAttribute>([NotNull]IEnumerable<Type> drawerOrEditorTypes, [NotNull]FieldInfo targetTypeField, [CanBeNull]FieldInfo useForChildrenField, [NotNull]ref Dictionary<Type,Type> addEditorsByType, bool targetMustBeUnityObject, bool canBeAbstract) where TAttribute : Attribute
		{
			var attType = typeof(TAttribute);

			foreach(var drawerType in drawerOrEditorTypes)
			{
				#if DEV_MODE
				Debug.Assert(!drawerType.IsAbstract);
				#endif

				//if(!drawerType.IsAbstract)
				{
					var attributes = drawerType.GetCustomAttributes(attType, true);
					for(int a = attributes.Length - 1; a >= 0; a--)
					{
						var attribute = attributes[a];
						bool useForChildren = useForChildrenField == null ? true : (bool)useForChildrenField.GetValue(attribute);
						if(useForChildren)
						{
							var targetType = targetTypeField.GetValue(attribute) as Type;
							if(!targetType.IsClass)
							{
								if(!targetType.IsInterface)
								{
									//value types don't support inheritance
									continue;
								}

								var implementingTypes = targetMustBeUnityObject ? targetType.GetImplementingUnityObjectTypes(true, canBeAbstract) : targetType.GetImplementingTypes(true, canBeAbstract);

								#if DEV_MODE && DEBUG_INTERFACE_SUPPORT
								Debug.Log("interface "+targetType.Name+" implementing types: "+StringUtils.ToString(implementingTypes));
								#endif

								for(int t = implementingTypes.Length - 1; t >= 0; t--)
								{
									var implementingType = implementingTypes[t];
									if(!implementingType.IsAbstract || canBeAbstract)
									{
										if(!addEditorsByType.ContainsKey(implementingType))
										{
											#if DEV_MODE && DEBUG_INTERFACE_SUPPORT
											Debug.Log("Adding interface "+targetType.Name+" implementing type "+StringUtils.ToString(implementingType) +"...");
											#endif

											addEditorsByType.Add(implementingType, drawerType);
										}
									}
								}

								#if SUPPORT_EDITORS_FOR_INTERFACES
								if(targetMustBeUnityObject)
								{
									addEditorsByType.Add(targetType, drawerType);
								}
								#endif
							}
							else
							{
								var extendingTypes = targetMustBeUnityObject ? targetType.GetExtendingUnityObjectTypes(true, canBeAbstract) : targetType.GetExtendingTypes(true, canBeAbstract);
								for(int t = extendingTypes.Length - 1; t >= 0; t--)
								{
									var extendingType = extendingTypes[t];
									if(!extendingType.IsAbstract || canBeAbstract)
									{
										if(!addEditorsByType.ContainsKey(extendingType))
										{
											addEditorsByType.Add(extendingType, drawerType);
										}
									}
								}
							}
						}
						#if DEV_MODE
						else if(typeof(DecoratorDrawer).IsAssignableFrom(drawerType)) { Debug.LogWarning(drawerType.Name+ ".useForChildren was "+StringUtils.False); }
						#endif
					}
				}
			}
		}

		public static void BeginEditor(out bool editingTextFieldWas, out EventType eventType, out KeyCode keyCode)
		{
			BeginEditorOrPropertyDrawer(out editingTextFieldWas, out eventType, out keyCode);
		}

		public static void EndEditor(bool editingTextFieldWas, EventType eventType, KeyCode keyCode)
		{
			EndEditorOrPropertyDrawer(editingTextFieldWas, eventType, keyCode);
		}

		public static void BeginPropertyDrawer(out bool editingTextFieldWas, out EventType eventType, out KeyCode keyCode)
		{
			BeginEditorOrPropertyDrawer(out editingTextFieldWas, out eventType, out keyCode);
		}
		

		public static void EndPropertyDrawer(bool editingTextFieldWas, EventType eventType, KeyCode keyCode)
		{
			EndEditorOrPropertyDrawer(editingTextFieldWas, eventType, keyCode);
		}

		private static void BeginEditorOrPropertyDrawer(out bool editingTextFieldWas, out EventType eventType, out KeyCode keyCode)
		{
			editingTextFieldWas = EditorGUIUtility.editingTextField;
			eventType = DrawGUI.LastInputEventType;
			var lastInputEvent = DrawGUI.LastInputEvent();
			keyCode = lastInputEvent == null ? KeyCode.None : lastInputEvent.keyCode;
		}

		private static void EndEditorOrPropertyDrawer(bool editingTextFieldWas, EventType eventType, KeyCode keyCode)
		{
			if(EditorGUIUtility.editingTextField != editingTextFieldWas)
			{
				if(eventType != EventType.KeyDown && eventType != EventType.KeyUp)
				{
					#if DEV_MODE && DEBUG_SET_EDITING_TEXT_FIELD
					Debug.Log("DrawGUI.EditingTextField = "+StringUtils.ToColorizedString(EditorGUIUtility.editingTextField)+" with eventType="+StringUtils.ToString(eventType)+", keyCode="+keyCode+", lastInputEvent="+StringUtils.ToString(DrawGUI.LastInputEvent()));
					#endif
					DrawGUI.EditingTextField = EditorGUIUtility.editingTextField;
				}
				else
				{
					switch(keyCode)
					{
						case KeyCode.UpArrow:
						case KeyCode.DownArrow:
						case KeyCode.LeftArrow:
						case KeyCode.RightArrow:
							if(!EditorGUIUtility.editingTextField)
							{
								#if DEV_MODE
								Debug.Log("DrawGUI.EditingTextField = "+StringUtils.ToColorizedString(false)+" with eventType="+StringUtils.ToString(eventType)+", keyCode="+keyCode+", lastInputEvent="+StringUtils.ToString(DrawGUI.LastInputEvent()));
								#endif
								DrawGUI.EditingTextField = false;
							}
							else // prevent Unity automatically starting field editing when field focus is changed to a text field, as that is not how Power Inspector functions
							{
								#if DEV_MODE
								Debug.LogWarning("EditorGUIUtility.editingTextField = "+StringUtils.ToColorizedString(false)+" with eventType="+StringUtils.ToString(eventType)+", keyCode="+keyCode+", lastInputEvent="+StringUtils.ToString(DrawGUI.LastInputEvent()));
								#endif
								EditorGUIUtility.editingTextField = false;
							}
							return;
						default:
							#if DEV_MODE
							Debug.Log("DrawGUI.EditingTextField = "+StringUtils.ToColorizedString(false)+" with eventType="+StringUtils.ToString(eventType)+", keyCode="+keyCode+", lastInputEvent="+StringUtils.ToString(DrawGUI.LastInputEvent()));
							#endif
							DrawGUI.EditingTextField = EditorGUIUtility.editingTextField;
							return;
					}
				}
			}
		}
	}
}
#endif