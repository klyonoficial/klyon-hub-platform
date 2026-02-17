# Configuração do Oracle Cloud Object Storage

O Klyon Hub Platform pode usar **Oracle Cloud Object Storage** como provedor de armazenamento para fotos e documentos (contratos, recibos, declarações, NF importadas, etc.), utilizando a **S3 Compatibility API** da Oracle.

A implementação do **FTP** foi mantida e continua disponível; você pode alternar entre **Oracle**, **FTP** e **AWS S3** em **Admin > Configurações do Sistema > Armazenamento**.

---

## 1. No Oracle Cloud (Console)

### 1.1 Criar um bucket

1. Acesse o [Oracle Cloud Console](https://cloud.oracle.com).
2. **Menu** → **Storage** → **Buckets**.
3. Escolha o **compartment** e clique em **Create Bucket**.
4. Nome do bucket (ex.: `klyon-hub-storage`), storage tier **Standard**, clique em **Create**.
5. Anote o **Namespace** (ex.: `axabcdefghij`) — aparece no topo da página de Object Storage ou em **Bucket Details**.

### 1.2 Região (Region)

Anote o **identificador da região** (ex.: `sa-saopaulo-1`, `us-ashburn-1`).  
Lista oficial: [Regions and Availability Domains](https://docs.oracle.com/en-us/iaas/Content/General/Concepts/regions.htm).

### 1.3 Customer Secret Key (Access Key + Secret Key)

A API S3 Compatibility da Oracle usa um par **Access Key / Secret Key** (Customer Secret Key).

1. **Menu** (ícone do usuário, canto superior direito) → **User Settings**.
2. Na barra de menu horizontal: **Tokens and keys**.
3. **Generate Secret Key**.
4. **Display Name** (ex.: `klyon-hub-platform`), **Generate Secret Key**.
5. **Copie e guarde o Secret Key** — ela só é exibida uma vez.
6. Anote a **Access Key** (ex.: `0xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx`) que aparece na lista.

O usuário OCI usado deve ter permissão para ler/escrever no bucket. O próximo tópico explica como configurar isso.

### 1.4 Permissões: grupo e policy (ler/escrever no bucket)

No Oracle Cloud, o acesso é controlado por **grupos** e **policies** (políticas). O usuário em que você criou o Customer Secret Key precisa pertencer a um grupo que tenha permissão para gerenciar objetos no compartment onde está o bucket.

#### Passo 1: Criar um grupo (ou usar um existente)

Na interface atual do Oracle Cloud, **Groups** e **Users** ficam dentro de **Domains** (não aparecem direto em Identity).

1. No **Oracle Cloud Console**, abra o **Menu** (☰) → **Identity & Security** → **Identity** → **Domains**.
2. Clique no **nome do seu domínio de identidade** (ex.: o domínio padrão do tenancy).
3. No menu horizontal do domínio, clique em **User management**.
4. Clique em **Create Group**.
5. **Name**: por exemplo `ObjectStorageAppGroup`.
6. **Description** (opcional): ex. "Grupo para aplicações que usam Object Storage".
7. Clique em **Create**.

#### Passo 2: Adicionar o usuário ao grupo

1. Em **Identity** → **Domains** → (seu domínio) → **Groups**, clique no grupo que você criou (ex.: `ObjectStorageAppGroup`).
2. Na seção de membros do grupo, clique em **Add User to Group** (ou equivalente).
3. Selecione o **usuário** em que você criou o Customer Secret Key (o mesmo que usa para fazer login no Console, se for o caso).  
   *Se precisar criar ou ver usuários, use **Identity** → **Domains** → (seu domínio) → **Users**.*
4. Clique em **Add** (ou **OK**).

A partir daí, esse usuário herda as permissões concedidas ao grupo pelas policies.

#### Passo 3: Criar a policy que permite gerenciar objetos

As **Policies** ficam direto em Identity (como na sua tela).

1. **Menu** (☰) → **Identity & Security** → **Identity** → **Policies**.
2. Selecione o **compartment** onde a policy deve valer.  
   - Para aplicar ao compartment onde está o bucket: escolha esse compartment.  
   - Para aplicar a todo o tenancy: use o **root compartment** do tenancy.
3. Clique em **Create Policy**.
4. **Name**: ex. `AllowObjectStorageAppGroup`.
5. **Description** (opcional): ex. "Permite que o grupo da aplicação leia e escreva objetos no Object Storage".
6. **Compartment**: mantenha o mesmo compartment selecionado (ou root).
7. Em **Policy Builder**, escolha **Show manual editor** e cole uma das sentenças abaixo.

**Opção A — Bucket no compartment raiz (root):**

Se o bucket está no **mesmo compartment onde você está criando a policy** (ex.: o raiz, que aparece como `klyonoficial (root)`), use **`in tenancy`**:

```text
Allow group ObjectStorageAppGroup to manage objects in tenancy
```

*Se você usar `in compartment klyonoficial` com a policy no raiz, o OCI pode retornar erro "Compartment does not exist or is not part of the policy compartment subtree", porque ele procura um sub-compartment com esse nome. Para o compartment raiz, use `in tenancy`.*

**Opção B — Bucket em um sub-compartment:**

Se o bucket está em um **compartment filho** do raiz (ex.: `Production`, `Storage`), use o nome desse compartment:

```text
Allow group ObjectStorageAppGroup to manage objects in compartment Production
```

Substitua `Production` pelo **nome exato** do compartment onde está o bucket (não use o nome do raiz aqui). A policy deve estar criada no **root** ou em um compartment ancestral desse compartment.

8. Clique em **Create**.

#### Verificação rápida

- O **usuário** que tem o Customer Secret Key está no **grupo**.
- Existe uma **policy** no compartment (ou no tenancy) que faz:  
  `Allow group <seu-grupo> to manage objects in compartment <compartment>` (ou `in tenancy`).

Com isso, a aplicação (usando Access Key + Secret Key desse usuário) consegue ler e escrever objetos no bucket desse compartment.

**Resumo:** Grupo → usuário dentro do grupo → policy “Allow group X to manage objects in compartment Y”. O usuário em que você gerou o Customer Secret Key deve ser esse que está no grupo.

---

## 2. No sistema (Configurações do Sistema)

1. Acesse **Admin** → **Configurações do Sistema**.
2. Abra a seção **Armazenamento**.
3. Clique em **Usar Oracle Cloud** para definir o provedor como Oracle.
4. Edite cada chave abaixo (clique em **Editar** na linha correspondente):

| Chave | Descrição | Exemplo |
|-------|-----------|--------|
| **Oracle.OS.Namespace** | Namespace do tenancy | `axabcdefghij` |
| **Oracle.OS.Region** | Identificador da região OCI | `sa-saopaulo-1` |
| **Oracle.OS.BucketName** | Nome do bucket | `klyon-hub-storage` |
| **Oracle.OS.AccessKeyId** | Access Key do Customer Secret Key | `0xxxxxxxx...` |
| **Oracle.OS.SecretAccessKey** | Secret Key (copiada na criação) | *(valor secreto)* |
| **Oracle.OS.ContractsFolder** | Pasta (prefixo) para contratos/documentos | `contracts` |
| **Oracle.OS.PhotosFolder** | Pasta (prefixo) para fotos de coworkings | `coworking-photos` |

5. Salve os valores. As chaves **Oracle.OS.AccessKeyId** e **Oracle.OS.SecretAccessKey** são tratadas como sensíveis (mascaradas na listagem).

---

## 3. Endpoint utilizado

O sistema monta o endpoint da S3 Compatibility API no formato:

- **Path style:** `https://<namespace>.compat.objectstorage.<region>.oci.customer-oci.com`

Exemplo: `https://axabcdefghij.compat.objectstorage.sa-saopaulo-1.oci.customer-oci.com`

Referência: [Object Storage Amazon S3 Compatibility API](https://docs.oracle.com/en-us/iaas/Content/Object/Tasks/s3compatibleapi.htm).

---

## 4. Alternar entre Oracle, FTP e AWS

Na mesma tela **Configurações do Sistema > Armazenamento**:

- **Usar Oracle Cloud** — define `Storage.Provider` = `Oracle` e usa as chaves `Oracle.OS.*`.
- **Usar FTP** — define `Storage.Provider` = `FTP` e usa as chaves `FTP.*`.
- **Usar AWS S3** — define `Storage.Provider` = `AWS` e usa as chaves `AWS.S3.*`.

As configurações de cada provedor ficam salvas na tabela **SystemConfigurations**; ao trocar de provedor, apenas o valor de **Storage.Provider** é alterado.

---

## 5. Resumo das chaves na tabela SystemConfigurations

Todas as chaves de armazenamento ficam na categoria **Storage** e podem ser editadas pela tela de Configurações do Sistema:

- `Storage.Provider` — `Oracle`, `FTP` ou `AWS`
- `Oracle.OS.Namespace`
- `Oracle.OS.Region`
- `Oracle.OS.BucketName`
- `Oracle.OS.AccessKeyId` (sensível)
- `Oracle.OS.SecretAccessKey` (sensível)
- `Oracle.OS.ContractsFolder`
- `Oracle.OS.PhotosFolder`

Para novos ambientes, o seed do sistema já cria essas chaves com valores em branco (exceto pastas e região com valor padrão); basta preencher e clicar em **Usar Oracle Cloud**.
