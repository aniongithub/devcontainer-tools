all: clean restore build publish

clean:
	@echo "Clean starting..."
	dotnet clean
	@echo "Clean done."

restore:
	@echo "Restore starting..."
	dotnet restore
	@echo "Restore done."

build:
	@echo "Build starting..."
	dotnet build
	@echo "Build done."

publish:
	@echo "Publish starting..."	
	./publish.sh
	@echo "Publish done."