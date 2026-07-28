# Controller image for the cicd platform.
#
# Build context is the repo's `jenkins/` directory (NOT jenkins/controller/) so the EXISTING
# jenkins/jobs/cicd-jobs.groovy is baked in without duplication:
#   docker build -f jenkins/controller/Dockerfile -t cicd-jenkins:latest jenkins/
# The Aspire AppHost does this for you via AddDockerfile("jenkins", "../../../jenkins",
# "controller/Dockerfile").
#
# Deliberately NOT multi-stage: nothing here is compiled. jenkins-plugin-cli writes the plugins into
# the final image by design, so a builder stage would be theatre. (Documented exception to the
# multi-stage rule in CLAUDE.md.)

FROM jenkins/jenkins:lts-jdk21

USER root

# The docker CLI only — never the daemon. The Docker Pipeline plugin drives the HOST's daemon
# through the bind-mounted /var/run/docker.sock to launch sibling build-agent containers, which is
# what every jenkins/*/Jenkinsfile's `agent { docker { ... } }` block relies on.
RUN apt-get update \
 && apt-get install -y --no-install-recommends ca-certificates curl gnupg \
 && install -m 0755 -d /etc/apt/keyrings \
 && curl -fsSL https://download.docker.com/linux/debian/gpg -o /etc/apt/keyrings/docker.asc \
 && chmod a+r /etc/apt/keyrings/docker.asc \
 && echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/debian $(. /etc/os-release && echo "$VERSION_CODENAME") stable" \
      > /etc/apt/sources.list.d/docker.list \
 && apt-get update \
 && apt-get install -y --no-install-recommends docker-ce-cli \
 && rm -rf /var/lib/apt/lists/*

# Config lives OUTSIDE /var/jenkins_home on purpose. That path is a named volume, and a named volume
# is seeded from the image ONLY when it is first created — anything COPY'd under it would be frozen
# at first-boot content forever, silently ignoring every later edit to casc/ or plugins.txt.
RUN mkdir -p /var/jenkins_conf/casc /var/jenkins_conf/jobdsl \
 && chown -R jenkins:jenkins /var/jenkins_conf

USER jenkins

COPY --chown=jenkins:jenkins controller/plugins.txt /usr/share/jenkins/ref/plugins.txt
RUN jenkins-plugin-cli --plugin-file /usr/share/jenkins/ref/plugins.txt

COPY --chown=jenkins:jenkins controller/casc/ /var/jenkins_conf/casc/
COPY --chown=jenkins:jenkins jobs/            /var/jenkins_conf/jobdsl/

ENV JAVA_OPTS="-Djenkins.install.runSetupWizard=false -Djava.awt.headless=true" \
    CASC_JENKINS_CONFIG=/var/jenkins_conf/casc

EXPOSE 8080

# Generous start-period: a cold first boot installs nothing but does run JCasC + the Job DSL seed.
HEALTHCHECK --interval=15s --timeout=5s --start-period=90s --retries=20 \
  CMD curl -fsS http://localhost:8080/login >/dev/null || exit 1
