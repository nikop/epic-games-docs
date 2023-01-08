set -e
git submodule update --remote

dotnet run --configuration Release --project src

git add .
git commit -am "Updated" || true
git push || true