ECHO OFF

ECHO clean up ressources
docker rmi ai.hgb.runtime.repository.img:latest
docker rmi ai.hgb.runtime.broker.img:latest
docker rmi ai.hgb.runtime.languageservice.img:latest

ECHO publish and build images

Pushd .\Repository
dotnet publish -c Release
REM xcopy ".\base\*" ".\bin\Release\net8.0\" /Y
REM docker build -t ai.hgb-repository-img -f Dockerfile .
xcopy ".\packages\*" ".\bin\Release\net8.0\publish\" /Y
xcopy ".\packages\*" ".\bin\Release\net8.0\" /Y
docker build -f "C:\dev\workspaces\spa\Ai.Hgb.Runtime\src\Repository\Dockerfile" --force-rm -t ai.hgb.runtime.repository.img  --build-arg "BUILD_CONFIGURATION=Release" --label "com.microsoft.created-by=visual-studio" --label "com.microsoft.visual-studio.project-name=Repository" "C:\dev\workspaces\spa\Ai.Hgb.Runtime\src"
Popd

Pushd .\LanguageService
dotnet publish -c Release
docker build -f "C:\dev\workspaces\spa\Ai.Hgb.Runtime\src\LanguageService\Dockerfile" --force-rm -t ai.hgb.runtime.languageservice.img  --build-arg "BUILD_CONFIGURATION=Release" --label "com.microsoft.created-by=visual-studio" --label "com.microsoft.visual-studio.project-name=LanguageService" "C:\dev\workspaces\spa\Ai.Hgb.Runtime\src"
REM docker build -t ai.hgb.runtime-languageservice-img -f Dockerfile .
Popd

Pushd .\Broker
dotnet publish -c Release
xcopy ".\configurations\*" ".\bin\Release\net8.0\publish\" /Y
xcopy ".\configurations\*" ".\bin\Release\net8.0\" /Y
docker build -t ai.hgb.runtime.broker.img -f Dockerfile .
Popd