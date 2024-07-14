pushd ..\XCodeTool
dotnet publish -f net8.0 -r win-x64 -c Release --self-contained true /p:PublishSingleFile=true /p:EnableCompressionInSingleFile=true
popd