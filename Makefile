ROOT_DIR=$(shell dirname $(realpath $(firstword $(MAKEFILE_LIST))))

.PHONY: all publish package
all: restore clean build publish package

restore:
	@echo "Restore starting..."
	dotnet restore
	@echo "Restore done."

clean:
	@echo "Clean starting..."
	dotnet clean
	@echo "Clean done."

build:
	@echo "Build starting..."
	dotnet build /p:Version="$(shell cat version)"
	@echo "Build done."

publish:
	@echo "Publish starting..."	
	${ROOT_DIR}/publish
	@echo "Publish done."

install:
	@echo "Installing..."
	${ROOT_DIR}/install
	@echo "Installation complete"

package:
	@echo "Packaging..."
	${ROOT_DIR}/package
	@echo "Packaging complete"