# Choose the base image
FROM development-d06690c1d0c24483bc519a18adc5ee82-base

# Create the user
RUN groupadd --gid ${HOST_USER_GID} ${HOST_USER_NAME} \
    && useradd --uid ${HOST_USER_UID} --gid ${HOST_USER_GID} -m ${HOST_USER_NAME} \
    && mkdir -p /home/${HOST_USER_NAME}/.vscode-server /home/${HOST_USER_NAME}/.vscode-server-insiders \
    && chown ${HOST_USER_UID}:${HOST_USER_GID} /home/${HOST_USER_NAME}/.vscode-server* \
    # [Optional] Add sudo support
    && apt-get update && apt-get install -y sudo acl \
    && echo ${HOST_USER_NAME} ALL=\(root\) NOPASSWD:ALL > /etc/sudoers.d/${HOST_USER_NAME} \
    && chmod 0440 /etc/sudoers.d/${HOST_USER_NAME}

# Set the default user
USER ${HOST_USER_NAME}