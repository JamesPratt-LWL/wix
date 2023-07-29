// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolset.Core.Link
{
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

        public IntermediateSection EntrySection { get; }

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
            var directorySymbols = symbols.OfType<DirectorySymbol>();
            var referenceSymbols = symbols.OfType<WixSimpleReferenceSymbol>();

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
        }

        private void AddSymbol(IntermediateSymbol symbol)
        {
            this.Find.EntrySection.AddSymbol(symbol);

            var symbolWithSection = new SymbolWithSection(this.Find.EntrySection, symbol);
            var fullName = symbolWithSection.GetFullName();
            this.Find.SymbolsByName.Add(fullName, symbolWithSection);
        }
    }
}
