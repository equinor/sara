import { Table } from "@equinor/eds-core-react";
import styled from "styled-components";

const OverviewDenseTable = styled(Table)`
  width: 100%;
  font-size: 0.8rem;

  td,
  th {
    padding: 0.25rem 0.5rem;
  }
`;

export default OverviewDenseTable;
