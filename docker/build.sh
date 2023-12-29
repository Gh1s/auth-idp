#!/bin/bash

while getopts 'spr:' flag;
do
  case "${flag}" in
    s) stable="true"
       echo "Building with stable tag" ;;
    p) push="true"
       echo "Pushing after build";;
    r) IFS="," read -ra additional_registries <<< "${OPTARG}"
       echo "Additionnal registries: ${additional_registries[@]}";;
    *) echo "Unknown parameter passed: $1"; exit 1 ;;
  esac
done

LATEST_TAG=latest
STABLE_TAG=stable

export REGISTRY=${REGISTRY:-gcr.io/csb-anthos}
export IDP_REPOSITORY=auth/idp
export IDP_TAG=$LATEST_TAG
export SAMPLE_AUTHORIZATION_CODE_MVC_REPOSITORY=auth/samples/authorization-code-mvc
export SAMPLE_AUTHORIZATION_CODE_MVC_TAG=$LATEST_TAG
echo "Building images with the default '$LATEST_TAG' tag."
docker-compose -f docker-compose-build.yaml build

if [ "$stable" == "true" ]
  then
    echo "Tagging images with the the 'stable' tag."
    docker tag $REGISTRY/$IDP_REPOSITORY:$LATEST_TAG $REGISTRY/$IDP_REPOSITORY:$STABLE_TAG
    docker tag $REGISTRY/$SAMPLE_AUTHORIZATION_CODE_MVC_REPOSITORY:$LATEST_TAG $REGISTRY/$SAMPLE_AUTHORIZATION_CODE_MVC_REPOSITORY:$STABLE_TAG
fi

dotnet tool restore --tool-manifest ../.config/dotnet-tools.json
export IDP_VERSION=$(dotnet version -p ../src/Csb.Auth.Idp/Csb.Auth.Idp.csproj --show | awk '{print $3}')
export SAMPLE_AUTHORIZATION_CODE_MVC_VERSION=$(dotnet version -p ../samples/Csb.Auth.Samples.AuthorizationCodeMvc/Csb.Auth.Samples.AuthorizationCodeMvc.csproj --show | awk '{print $3}')
echo "Tagging images with their version tag."
docker tag $REGISTRY/$IDP_REPOSITORY:$LATEST_TAG $REGISTRY/$IDP_REPOSITORY:$IDP_VERSION
docker tag $REGISTRY/$SAMPLE_AUTHORIZATION_CODE_MVC_REPOSITORY:$LATEST_TAG $REGISTRY/$SAMPLE_AUTHORIZATION_CODE_MVC_REPOSITORY:$SAMPLE_AUTHORIZATION_CODE_MVC_VERSION

if [ "${#additional_registries[@]}" -gt "0" ]
  then
    echo "Tagging images with the additional registries."
    for additional_registry in "${additional_registries[@]}"; do
      docker tag $REGISTRY/$IDP_REPOSITORY:$LATEST_TAG $additional_registry/$IDP_REPOSITORY:$LATEST_TAG
      docker tag $REGISTRY/$SAMPLE_AUTHORIZATION_CODE_MVC_REPOSITORY:$LATEST_TAG $additional_registry/$SAMPLE_AUTHORIZATION_CODE_MVC_REPOSITORY:$LATEST_TAG
      if [ "$stable" == "true" ]
        then
          docker tag $REGISTRY/$IDP_REPOSITORY:$LATEST_TAG $additional_registry/$IDP_REPOSITORY:$STABLE_TAG
          docker tag $REGISTRY/$SAMPLE_AUTHORIZATION_CODE_MVC_REPOSITORY:$LATEST_TAG $additional_registry/$SAMPLE_AUTHORIZATION_CODE_MVC_REPOSITORY:$STABLE_TAG
      fi
      docker tag $REGISTRY/$IDP_REPOSITORY:$LATEST_TAG $additional_registry/$IDP_REPOSITORY:$IDP_VERSION
      docker tag $REGISTRY/$SAMPLE_AUTHORIZATION_CODE_MVC_REPOSITORY:$LATEST_TAG $additional_registry/$SAMPLE_AUTHORIZATION_CODE_MVC_REPOSITORY:$SAMPLE_AUTHORIZATION_CODE_MVC_VERSION
    done
fi

if [ "$push" == "true" ]
then
  echo "Pushing images."
  docker push $REGISTRY/$IDP_REPOSITORY --all-tags
  docker push $REGISTRY/$SAMPLE_AUTHORIZATION_CODE_MVC_REPOSITORY --all-tags
  if [ "${#additional_registries[@]}" -gt "0" ]
    then
      echo "Pushing images of the additional registries."
      for additional_registry in "${additional_registries[@]}"; do
        docker push $additional_registry/$IDP_REPOSITORY --all-tags
        docker push $additional_registry/$SAMPLE_AUTHORIZATION_CODE_MVC_REPOSITORY --all-tags
      done
  fi
fi