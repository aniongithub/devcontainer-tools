# Choose the base image
FROM aws-dev-477944f57fb141e191003bc408dca004-base

# Create the user
RUN groupadd --gid ${HOST_USER_GID} ${HOST_USER_NAME} \
    && useradd --uid ${HOST_USER_UID} --gid ${HOST_USER_GID} -m ${HOST_USER_NAME} \
    # Add sudo support
    && apt-get update && apt-get install -y sudo acl \
    && echo ${HOST_USER_NAME} ALL=\(root\) NOPASSWD:ALL > /etc/sudoers.d/${HOST_USER_NAME} \
    && chmod 0440 /etc/sudoers.d/${HOST_USER_NAME}

# Set the default user
USER ${HOST_USER_NAME}