// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolset.Core.Link
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using WixToolset.Data;
    using WixToolset.Data.Symbols;

    internal class AddDefaultSymbolsCommand
    {
        public static readonly string WixStandardInstallFolder = "INSTALLFOLDER";
        public static readonly string WixStandardInstallFolderParent = "ProgramFiles6432Folder";
        public static readonly string WixStandardInstallFolderReference = "Directory:INSTALLFOLDER";

        public AddDefaultSymbolsCommand(FindEntrySectionAndLoadSymbolsCommand find, List<IntermediateSection> sections)
        {
            this.Find = find;
            this.Sections = sections;
        }

        public IEnumerable<IntermediateSection> Sections { get; }

        public FindEntrySectionAndLoadSymbolsCommand Find { get; }

        public void Execute()
        {
            if (this.Find.EntrySection.Type != SectionType.Package)
            {
                // Only packages...for now.
                return;
            }

            var symbols = this.Sections.SelectMany(s => s.Symbols);
            var referenceSymbols = symbols.OfType<WixSimpleReferenceSymbol>();
            var directorySymbols = symbols.OfType<DirectorySymbol>();

            if (referenceSymbols.Any(s => s.SymbolicName == WixStandardInstallFolderReference)
                && !directorySymbols.Any(d => d.Id.Id == WixStandardInstallFolder))
            {
                // If there are any INSTALLFOLDER references, add a default one, using the
                // first reference as the "canonical" reference for source line numbers.
                this.AddSymbol(new DirectorySymbol(null, new Identifier(AccessModifier.Global, WixStandardInstallFolder))
                {
                    ParentDirectoryRef = WixStandardInstallFolderParent,
                    Name = "!(bind.Property.Manufacturer) !(bind.Property.ProductName)",
                    SourceName = ".",
                });

                this.AddSymbol(new WixSimpleReferenceSymbol(null, new Identifier(AccessModifier.Global, WixStandardInstallFolder))
                {
                    Table = "Directory",
                    PrimaryKeys = WixStandardInstallFolderParent,
                });
            }

            var upgradeSymbols = symbols.OfType<UpgradeSymbol>();
            if (!upgradeSymbols.Any())
            {
                var packageSymbol = this.Find.EntrySection.Symbols.OfType<WixPackageSymbol>().FirstOrDefault();

                if (packageSymbol?.UpgradeStrategy == WixPackageUpgradeStrategy.MajorUpgrade
                    && !String.IsNullOrEmpty(packageSymbol?.UpgradeCode))
                {
                    this.AddDefaultMajorUpgrade(packageSymbol);
                }

            }
        }

        private void AddDefaultMajorUpgrade(WixPackageSymbol packageSymbol)
        {
            this.AddSymbol(new UpgradeSymbol(packageSymbol.SourceLineNumbers)
            {
                UpgradeCode = packageSymbol.UpgradeCode,
                MigrateFeatures = true,
                ActionProperty = WixUpgradeConstants.UpgradeDetectedProperty,
                VersionMax = packageSymbol.Version,
                Language = packageSymbol.Language,
            });

            this.AddSymbol(new UpgradeSymbol(packageSymbol.SourceLineNumbers)
            {
                UpgradeCode = packageSymbol.UpgradeCode,
                VersionMin = packageSymbol.Version,
                Language = packageSymbol.Language,
                OnlyDetect = true,
                ActionProperty = WixUpgradeConstants.DowngradeDetectedProperty,
            });

            this.AddSymbol(new LaunchConditionSymbol(packageSymbol.SourceLineNumbers)
            {
                Condition = WixUpgradeConstants.DowngradePreventedCondition,
                Description = "!(loc.WixDowngradePreventedMessage)",
            });

            this.CreateActionSymbol(this.Find.EntrySection, packageSymbol.SourceLineNumbers, SequenceTable.InstallExecuteSequence, "RemoveExistingProducts", "InstallValidate");
        }

        public WixActionSymbol CreateActionSymbol(IntermediateSection section, SourceLineNumber sourceLineNumbers, SequenceTable sequence, string actionName, string afterAction)
        {
            var actionId = new Identifier(AccessModifier.Global, sequence, actionName);

            var actionSymbol = section.AddSymbol(new WixActionSymbol(sourceLineNumbers, actionId)
            {
                SequenceTable = sequence,
                Action = actionName,
                After = afterAction,
            });

            section.AddSymbol(new WixSimpleReferenceSymbol(sourceLineNumbers)
            {
                Table = SymbolDefinitions.WixAction.Name,
                PrimaryKeys = $"{sequence}/{afterAction}",
            });

            return actionSymbol;
        }

        private void AddSymbol(IntermediateSymbol symbol)
        {
            this.Find.EntrySection.AddSymbol(symbol);

            if (!String.IsNullOrEmpty(symbol.Id?.Id))
            {
                var symbolWithSection = new SymbolWithSection(this.Find.EntrySection, symbol);
                var fullName = symbolWithSection.GetFullName();

                this.Find.SymbolsByName.Add(fullName, symbolWithSection);
            }
        }
    }
}
