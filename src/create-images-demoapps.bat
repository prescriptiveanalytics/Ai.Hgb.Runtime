ECHO OFF

ECHO clean up ressources
docker rmi ai.hgb.application.demoapps.producer.img:latest
docker rmi ai.hgb.application.demoapps.consumer.img:latest
docker rmi ai.hgb.application.demoapps.prosumer.img:latest
docker rmi ai.hgb.application.demoapps.performancetest.img:latest

ECHO publish and build images

Pushd .\DemoApps\Producer
dotnet publish -c Release
xcopy ".\configurations\*" ".\bin\Release\net8.0\publish\" /Y
xcopy ".\configurations\*" ".\bin\Release\net8.0\" /Y
docker build -t ai.hgb.application.demoapps.producer.img -f Producer.Dockerfile .
Popd

Pushd .\DemoApps\Consumer
dotnet publish -c Release
xcopy ".\configurations\*" ".\bin\Release\net8.0\publish\" /Y
xcopy ".\configurations\*" ".\bin\Release\net8.0\" /Y
docker build -t ai.hgb.application.demoapps.consumer.img -f Consumer.Dockerfile .
Popd

Pushd .\DemoApps\Prosumer
dotnet publish -c Release
xcopy ".\configurations\*" ".\bin\Release\net8.0\publish\" /Y
xcopy ".\configurations\*" ".\bin\Release\net8.0\" /Y
docker build -t ai.hgb.application.demoapps.prosumer.img -f Prosumer.Dockerfile .
Popd

Pushd .\DemoApps\PerformanceTest
dotnet publish -c Release
xcopy ".\configurations\*" ".\bin\Release\net8.0\publish\" /Y
xcopy ".\configurations\*" ".\bin\Release\net8.0\" /Y
docker build -t ai.hgb.application.demoapps.performancetest.img -f PerformanceTest.Dockerfile .
Popd
