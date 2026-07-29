FROM myoung34/github-runner:ubuntu-noble@sha256:881a6b81df476e9b9ce7f9451b0efbacba7496ac4483613e24849a2c9b1ffd60

ARG TARGETARCH
ARG VCS_REF=unknown

LABEL org.opencontainers.image.source="https://github.com/ncosentino/foundry" \
      org.opencontainers.image.revision="${VCS_REF}" \
      org.opencontainers.image.title="Foundry CI runner" \
      org.opencontainers.image.description="PitCrew-compatible Foundry CI worker with exact .NET SDKs and NativeAOT prerequisites."

SHELL ["/bin/bash", "-o", "pipefail", "-c"]

ENV DOTNET_ROOT=/opt/dotnet \
    DOTNET_MULTILEVEL_LOOKUP=0 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    DOTNET_NOLOGO=true \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    PATH=/opt/dotnet:${PATH}

RUN test "${TARGETARCH}" = "amd64" \
    && apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install --yes --no-install-recommends \
        ca-certificates \
        clang \
        curl \
        file \
        zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*

RUN mkdir -p "${DOTNET_ROOT}" \
    && curl --fail --location --retry 5 \
        https://builds.dotnet.microsoft.com/dotnet/Sdk/9.0.316/dotnet-sdk-9.0.316-linux-x64.tar.gz \
        --output /tmp/dotnet-sdk-9.0.316-linux-x64.tar.gz \
    && printf '%s  %s\n' \
        '5a8558afd648c14a835e00ae08fa556083f50e3ada164d3e73293fcd4850b0519a27c11f2dae95a9bbe4af432be33bf14451ef11ba69527e34f9cf3077a1c2b5' \
        '/tmp/dotnet-sdk-9.0.316-linux-x64.tar.gz' \
        | sha512sum --check --strict \
    && tar --extract --gzip --file /tmp/dotnet-sdk-9.0.316-linux-x64.tar.gz --directory "${DOTNET_ROOT}" \
    && rm /tmp/dotnet-sdk-9.0.316-linux-x64.tar.gz

RUN curl --fail --location --retry 5 \
        https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.302/dotnet-sdk-10.0.302-linux-x64.tar.gz \
        --output /tmp/dotnet-sdk-10.0.302-linux-x64.tar.gz \
    && printf '%s  %s\n' \
        '10069bec8783596484a610332f090d562802a41b9b40e3327a5a5688b572e10c296ae300f940d40461f23c157ed1b0843c2f8e6b3f20d8d8d9d83432d8143bac' \
        '/tmp/dotnet-sdk-10.0.302-linux-x64.tar.gz' \
        | sha512sum --check --strict \
    && tar --extract --gzip --file /tmp/dotnet-sdk-10.0.302-linux-x64.tar.gz --directory "${DOTNET_ROOT}" \
    && rm /tmp/dotnet-sdk-10.0.302-linux-x64.tar.gz

RUN test -x /actions-runner/bin/Runner.Listener \
    && dotnet --list-sdks | grep -F '9.0.316' \
    && dotnet --list-sdks | grep -F '10.0.302' \
    && clang --version \
    && file --version \
    && pwsh --version \
    && git --version \
    && gh --version
