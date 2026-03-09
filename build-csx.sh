#!/bin/bash

# Paper CSX Compiler Build Script
# Compiles all .csx files to C# source files

CSX_CLI_PATH="Paper.CSX.Cli/bin/Debug/net10.0/Paper.CSX.Cli"
CSX_FILES=$(find . -name "*.csx" | sort)

# Build the CSX CLI if it doesn't exist
if [ ! -f "$CSX_CLI_PATH" ]; then
    echo "Building CSX CLI..."
    dotnet build Paper.CSX.Cli -c Debug
    if [ ! -f "$CSX_CLI_PATH" ]; then
        echo "Failed to build CSX CLI" >&2
        exit 1
    fi
fi

# Compile all CSX files
echo "Compiling CSX files..."
for FILE in $CSX_FILES; do
    echo "Compiling: $FILE"
    dotnet run --project Paper.CSX.Cli -- parse "$FILE"
done

echo "CSX compilation completed!"
