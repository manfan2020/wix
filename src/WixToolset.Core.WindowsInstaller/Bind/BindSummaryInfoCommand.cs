// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolset.Core.WindowsInstaller.Bind
{
    using System;
    using System.Globalization;
    using System.Linq;
    using WixToolset.Data;
    using WixToolset.Data.Tuples;

    /// <summary>
    /// Binds the summary information table of a database.
    /// </summary>
    internal class BindSummaryInfoCommand
    {
        public BindSummaryInfoCommand(IntermediateSection section)
        {
            this.Section = section;
        }

        private IntermediateSection Section { get; }

        /// <summary>
        /// Returns a flag indicating if files are compressed by default.
        /// </summary>
        public bool Compressed { get; private set; }

        /// <summary>
        /// Returns a flag indicating if uncompressed files use long filenames.
        /// </summary>
        public bool LongNames { get; private set; }

        public int InstallerVersion { get; private set; }

        /// <summary>
        /// Modularization guid, or null if the output is not a module.
        /// </summary>
        public string ModularizationSuffix { get; private set; }

        public void Execute()
        {
            this.Compressed = false;
            this.LongNames = false;
            this.InstallerVersion = 0;
            this.ModularizationSuffix = null;

            var foundCreateDataTime = false;
            var foundLastSaveDataTime = false;
            var foundCreatingApplication = false;
            var now = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture);

            foreach (var summaryInformationTuple in this.Section.Tuples.OfType<SummaryInformationTuple>())
            {
                switch (summaryInformationTuple.PropertyId)
                {
                    case SummaryInformationType.Codepage: // PID_CODEPAGE
                        // make sure the code page is an int and not a web name or null
                        var codepage = summaryInformationTuple.Value;

                        if (String.IsNullOrEmpty(codepage))
                        {
                            codepage = "0";
                        }
                        else
                        {
                            summaryInformationTuple.Value = Common.GetValidCodePage(codepage, false, false, summaryInformationTuple.SourceLineNumbers).ToString(CultureInfo.InvariantCulture);
                        }
                        break;
                    case SummaryInformationType.PackageCode: // PID_REVNUMBER
                        var packageCode = summaryInformationTuple.Value;

                        if (SectionType.Module == this.Section.Type)
                        {
                            this.ModularizationSuffix = "." + packageCode.Substring(1, 36).Replace('-', '_');
                        }
                        else if ("*" == packageCode)
                        {
                            // set the revision number (package/patch code) if it should be automatically generated
                            summaryInformationTuple.Value = Common.GenerateGuid();
                        }
                        break;
                    case SummaryInformationType.Created:
                        foundCreateDataTime = true;
                        break;
                    case SummaryInformationType.LastSaved:
                        foundLastSaveDataTime = true;
                        break;
                    case SummaryInformationType.WindowsInstallerVersion:
                        this.InstallerVersion = summaryInformationTuple[SummaryInformationTupleFields.Value].AsNumber();
                        break;
                    case SummaryInformationType.WordCount:
                        if (SectionType.Patch == this.Section.Type)
                        {
                            this.LongNames = true;
                            this.Compressed = true;
                        }
                        else
                        {
                            var attributes = summaryInformationTuple[SummaryInformationTupleFields.Value].AsNumber();
                            this.LongNames = (0 == (attributes & 1));
                            this.Compressed = (2 == (attributes & 2));
                        }
                        break;
                    case SummaryInformationType.CreatingApplication: // PID_APPNAME
                        foundCreatingApplication = true;
                        break;
                }
            }

            // add a summary information row for the create time/date property if its not already set
            if (!foundCreateDataTime)
            {
                this.Section.AddTuple(new SummaryInformationTuple(null)
                {
                    PropertyId = SummaryInformationType.Created,
                    Value = now,
                });
            }

            // add a summary information row for the last save time/date property if its not already set
            if (!foundLastSaveDataTime)
            {
                this.Section.AddTuple(new SummaryInformationTuple(null)
                {
                    PropertyId = SummaryInformationType.LastSaved,
                    Value = now,
                });
            }

            // add a summary information row for the creating application property if its not already set
            if (!foundCreatingApplication)
            {
                this.Section.AddTuple(new SummaryInformationTuple(null)
                {
                    PropertyId = SummaryInformationType.CreatingApplication,
                    Value = String.Format(CultureInfo.InvariantCulture, AppCommon.GetCreatingApplicationString()),
                });
            }
        }
    }
}
