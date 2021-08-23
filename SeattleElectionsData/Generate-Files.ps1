$initialPath = $args[0]
$neighborhoodsPath = $args[1]
$inputDirectory = [System.IO.Path]::GetDirectoryName($initialPath)
$initialFileName = [System.IO.Path]::GetFileName($initialPath)
dotnet run -p ../utils/FilterPrecinctResults/FilterPrecinctResults.csproj $initialPath
$filteredPath = [System.IO.Path]::Combine($inputDirectory, "seattle-" + $initialFileName)
dotnet run -p ../utils/CreateJsonPrecinctResults/CreateJsonPrecinctResults.csproj $filteredPath
$convertedFileName = [System.IO.Path]::GetFileNameWithoutExtension($filteredPath)
$convertedPath = [System.IO.Path]::Combine($inputDirectory, "converted-" + $convertedFileName + ".json")
dotnet run -p ../utils/CreateJsonNeighborhoodResults/CreateJsonNeighborhoodResults.csproj $neighborhoodsPath $convertedPath
