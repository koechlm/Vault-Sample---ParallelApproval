/*=====================================================================
  
  This file is part of the Autodesk Vault API Code Samples.

  Copyright (C) Autodesk Inc.  All rights reserved.

THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY
KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
PARTICULAR PURPOSE.
=====================================================================*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace RestrictOperations
{

    [XmlRoot]
    public class RestrictSettings
    {
        public List<string> RestrictedOperations;

        public RestrictSettings()
        {
            RestrictedOperations = new List<string>();
        }

        // figure out the path to this extension
        public static string GetSettingsPath(string mSettingsFile)
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            path = Path.Combine(path, @"Autodesk\Vault 2017\Extensions\RestrictOperations");
            return Path.Combine(path, mSettingsFile);
        }

        public static RestrictSettings Load()
        {
            RestrictSettings retVal = null;
            try
            {
                using (System.IO.StreamReader reader = new System.IO.StreamReader(GetSettingsPath("RestrictSettings.xml")))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(RestrictSettings));
                    retVal = (RestrictSettings)serializer.Deserialize(reader);
                }
            }
            catch
            { }

            // if we run into an error, just return a blank settings
            if (retVal == null)
                retVal = new RestrictSettings();

            return retVal;
        }

        public void Save()
        {
            try
            {
                using (FileStream writer = new FileStream(GetSettingsPath("RestrictSettings.xml"), FileMode.Create))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(RestrictSettings));
                    serializer.Serialize(writer, this);
                }
            }
            catch
            { }
        }
    }
}
