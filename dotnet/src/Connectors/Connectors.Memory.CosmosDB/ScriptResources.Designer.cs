﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Microsoft.SemanticKernel.Connectors.Memory.Cosmos {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class ScriptResources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal ScriptResources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Microsoft.SemanticKernel.Connectors.Memory.Cosmos.ScriptResources", typeof(ScriptResources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to function userDefinedFunction(input1, input2)
        ///{
        ///    var arrayInput1=JSON.parse(input1);
        ///    var arrayInput2=JSON.parse(input2);
        ///    if (!Array.isArray(arrayInput1) || !Array.isArray(arrayInput2))
        ///    {
        ///        throw new Error(&quot;intput1 or input2 not an array string&quot;);
        ///    }
        ///    if (arrayInput1.length!=arrayInput2.length)
        ///    {
        ///        return 0;
        ///    }
        ///    const dotProduct = arrayInput1.reduce((acc, val, i) =&gt; acc + val * arrayInput2[i], 0);
        ///    const magnitudeA = Math.sqrt(arrayInput1.reduce((acc, val) =&gt; acc  [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string UDF_CosinSimularity {
            get {
                return ResourceManager.GetString("UDF_CosinSimularity", resourceCulture);
            }
        }
    }
}
